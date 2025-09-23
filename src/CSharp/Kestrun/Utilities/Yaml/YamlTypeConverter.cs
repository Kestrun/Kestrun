using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Core;
using System.Collections.Specialized;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// Utility class for converting YAML nodes to appropriate .NET types
/// </summary>
public static partial class YamlTypeConverter
{
    // YAML 1.2: .inf, +.inf, -.inf, and permissive "inf" / "infinity"
    private static readonly Regex InfinityRegex =
        MyRegex();

    /// <summary>
    ///     Convert a YamlNode to the most appropriate .NET type based on its tag and content
    /// </summary>
    /// <param name="node">The YAML node to convert.</param>
    /// <returns>The converted .NET object, or null if the node is null.</returns>
    /// <exception cref="FormatException">Thrown when the node's value cannot be converted to the appropriate type.</exception>
    public static object? ConvertValueToProperType(YamlNode node)
    {
        // Only scalars are in scope; return other node types unchanged
        if (node is not YamlScalarNode scalar)
        {
            return node;
        }

        // TagName in YamlDotNet v16 may be non-specific ("!"), in which case accessing .Value throws.
        // Only treat it as a usable tag when it is NOT empty and NOT non-specific.
        string? tag = null;
        try
        {
            if (!scalar.Tag.IsEmpty && !scalar.Tag.IsNonSpecific)
            {
                tag = scalar.Tag.Value; // e.g., "tag:yaml.org,2002:int"
            }
        }
        catch
        {
            // Defensive: ignore tag retrieval issues; we'll fall back to heuristics.
            tag = null;
        }

        var value = scalar.Value;       // underlying string representation

        // If for some reason we don't have a string, return as-is
        if (value is null)
        {
            return scalar;
        }

        // If a well-known tag is present, honor it first
        if (!string.IsNullOrEmpty(tag))
        {
            switch (tag)
            {
                case "tag:yaml.org,2002:str":
                    return value;

                case "tag:yaml.org,2002:null":
                    return null;

                case "tag:yaml.org,2002:bool":
                    if (bool.TryParse(value, out var b))
                    {
                        return b;
                    }

                    throw new FormatException($"Failed to parse scalar '{value}' as boolean.");

                case "tag:yaml.org,2002:int":
                    return ParseYamlInt(value);

                case "tag:yaml.org,2002:float":
                    // ±.inf / inf / infinity
                    if (InfinityRegex.IsMatch(value))
                    {
                        return value.StartsWith("-", StringComparison.Ordinal) ? double.NegativeInfinity : double.PositiveInfinity;
                    }

                    if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec))
                    {
                        return dec;
                    }

                    throw new FormatException($"Failed to parse scalar '{value}' as decimal.");

                case "tag:yaml.org,2002:timestamp":
                    // Tests expect System.DateTime (dropping offset). Parse using DateTimeOffset first, then return DateTime component.
                    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                                                DateTimeStyles.RoundtripKind, out var dto))
                    {
                        return dto.DateTime;
                    }
                    if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                          DateTimeStyles.RoundtripKind, out var dt))
                    {
                        return dt;
                    }
                    throw new FormatException($"Failed to parse scalar '{value}' as DateTime.");
            }
        }

        // No (or unknown) tag — apply plain-style heuristics like your PowerShell version
        if (scalar.Style == ScalarStyle.Plain)
        {
            // Booleans
            if (bool.TryParse(value, out var bPlain))
            {
                return bPlain;
            }

            // Integers (promote via BigInteger, then downcast if safe)
            if (TryParseBigInteger(value, out var bigInt))
            {
#pragma warning disable IDE0078
                if (bigInt >= int.MinValue && bigInt <= int.MaxValue)
                {
                    return (int)bigInt;
                }

                if (bigInt >= long.MinValue && bigInt <= long.MaxValue)
                {
                    return (long)bigInt;
                }
#pragma warning restore IDE0078
                return bigInt; // keep as BigInteger
            }

            // Floats (try decimal, then double)
            if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dPlain))
            {
                return dPlain;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblPlain))
            {
                return dblPlain;
            }
        }

        // Explicit YAML null tokens in plain style
        if (scalar.Style == ScalarStyle.Plain &&
            (value == string.Empty || value == "~" ||
             string.Equals(value, "null", StringComparison.Ordinal) ||
             string.Equals(value, "Null", StringComparison.Ordinal) ||
             string.Equals(value, "NULL", StringComparison.Ordinal)))
        {
            return null;
        }

        // Fallback: leave it as a string
        return value;
    }

    private static object ParseYamlInt(string value)
    {
        // Handle scientific notation integers like 1e+3 or -2E-1 (only when mantissa is integer and exponent >= 0)
        // Tests only require positive exponent producing integral result.
        // Pattern: ^[+-]?\d+[eE][+-]?\d+$
        if (ScientificIntRegex().IsMatch(value))
        {
            try
            {
                var parts = value.Split('e', 'E');
                var mantissaStr = parts[0];
                var expStr = parts[1];
                var exp = int.Parse(expStr, CultureInfo.InvariantCulture);
                // If exponent negative, result would be fractional; treat as format error for !!int
                if (exp < 0)
                {
                    throw new FormatException($"Failed to parse scalar '{value}' as integer (negative exponent not integral).");
                }
                var sign = 1;
                if (mantissaStr.StartsWith("+", StringComparison.Ordinal))
                {
                    mantissaStr = mantissaStr[1..];
                }
                else if (mantissaStr.StartsWith("-", StringComparison.Ordinal))
                {
                    sign = -1;
                    mantissaStr = mantissaStr[1..];
                }

                // Remove leading zeros to avoid BigInteger mis-interpretation (not strictly needed but clean)
                if (mantissaStr.Length > 1 && mantissaStr.All(c => c == '0'))
                {
                    mantissaStr = "0"; // all zeros
                }

                if (!BigInteger.TryParse(mantissaStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mantissa))
                {
                    throw new FormatException($"Failed to parse scalar '{value}' as integer (mantissa invalid).");
                }
                // 10^exp
                var pow = Pow10(exp);
                var bigVal = mantissa * pow * sign;
                // Downcast if possible
                if (bigVal >= int.MinValue && bigVal <= int.MaxValue)
                {
                    return (int)bigVal;
                }
                if (bigVal >= long.MinValue && bigVal <= long.MaxValue)
                {
                    return (long)bigVal;
                }
                return bigVal;
            }
            catch (Exception ex)
            {
                throw new FormatException($"Failed to parse scalar '{value}' as integer (scientific).", ex);
            }
        }
        // Base prefixes: 0o (octal), 0x (hex). Otherwise parse as generic integer.
        if (value.Length > 2)
        {
            var prefix = value[..2];
            if (prefix.Equals("0o", StringComparison.OrdinalIgnoreCase))
            {
                var asLong = Convert.ToInt64(value[2..], 8);
                return DowncastInteger(asLong);
            }
            if (prefix.Equals("0x", StringComparison.OrdinalIgnoreCase))
            {
                var asLong = Convert.ToInt64(value[2..], 16);
                return DowncastInteger(asLong);
            }
        }

        if (!TryParseBigInteger(value, out var big))
        {
            throw new FormatException($"Failed to parse scalar '{value}' as integer.");
        }
#pragma warning disable IDE0078
        // Try to downcast
        if ((big >= int.MinValue) && (big <= int.MaxValue))
        {
            return (int)big;
        }

        if (big >= long.MinValue && big <= long.MaxValue)
        {
            return (long)big;
        }
#pragma warning restore IDE0078

        return big; // keep BigInteger if it doesn't fit
    }

    private static bool TryParseBigInteger(string s, out BigInteger result)
    {
        // Recognize scientific integer in plain style (e.g., 1e+3) where exponent >= 0.
        if (ScientificIntRegex().IsMatch(s))
        {
            try
            {
                var parts = s.Split('e', 'E');
                var mantissaStr = parts[0];
                var expStr = parts[1];
                var exp = int.Parse(expStr, CultureInfo.InvariantCulture);
                if (exp < 0)
                {
                    result = default;
                    return false; // would be fractional
                }
                var sign = 1;
                if (mantissaStr.StartsWith("+", StringComparison.Ordinal))
                {
                    mantissaStr = mantissaStr[1..];
                }
                else if (mantissaStr.StartsWith("-", StringComparison.Ordinal))
                {
                    sign = -1;
                    mantissaStr = mantissaStr[1..];
                }
                if (!BigInteger.TryParse(mantissaStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mantissa))
                {
                    result = default; return false;
                }
                var pow = Pow10(exp);
                result = mantissa * pow * sign;
                return true;
            }
            catch
            {
                // fallthrough to normal parse
            }
        }
        // Mirror PS code using Float|Integer flags (allow underscores not by default—YamlDotNet usually strips them)
        return BigInteger.TryParse(
            s,
            NumberStyles.Integer | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, // keep parity with original
            CultureInfo.InvariantCulture,
            out result
        );
    }

    private static object DowncastInteger(long v)
    {
        return v is >= int.MinValue and <= int.MaxValue ? (int)v : (object)v;
    }

    /// <summary>
    /// Convert a YamlMappingNode to a dictionary. If <paramref name="ordered"/> is true,
    /// returns an OrderedDictionary; otherwise a Dictionary&lt;string,object?&gt;.
    /// </summary>
    public static object ConvertYamlMappingToHashtable(YamlMappingNode node, bool ordered = false)
    {
        if (ordered)
        {
            var ret = new OrderedDictionary(StringComparer.Ordinal);
            foreach (var kv in node.Children)
            {
                var key = KeyToString(kv.Key);
                var val = ConvertYamlDocumentToPSObject(kv.Value, ordered);
                ret[key] = val;
            }
            return ret;
        }
        else
        {
            var ret = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kv in node.Children)
            {
                var key = KeyToString(kv.Key);
                ret[key] = ConvertYamlDocumentToPSObject(kv.Value, ordered);
            }
            return ret;
        }
    }

    /// <summary>
    /// Convert a YamlSequenceNode to an array (object?[]), preserving element order.
    /// </summary>
    public static object?[] ConvertYamlSequenceToArray(YamlSequenceNode node, bool ordered = false)
    {
        var list = new List<object?>(node.Children.Count);
        foreach (var child in node.Children)
        {
            list.Add(ConvertYamlDocumentToPSObject(child, ordered));
        }
        return [.. list];
    }

    /// <summary>
    /// Dispatcher that mirrors Convert-YamlDocumentToPSObject:
    /// maps Mapping→Hashtable/(OrderedDictionary), Sequence→array, Scalar→typed value.
    /// </summary>
    public static object? ConvertYamlDocumentToPSObject(YamlNode node, bool ordered = false) =>
        node switch
        {
            YamlMappingNode m => ConvertYamlMappingToHashtable(m, ordered),
            YamlSequenceNode s => ConvertYamlSequenceToArray(s, ordered),
            YamlScalarNode => ConvertValueToProperType(node),
            _ => node // fallback: return the node itself
        };

    /// <summary>
    /// Convenience overload to defensively handle scenarios where (due to dynamic invocation from PowerShell)
    /// a KeyValuePair&lt;YamlNode,YamlNode&gt; is passed instead of just the Value node. We unwrap and delegate.
    /// This should not normally be necessary, but avoids brittle failures when reflection / dynamic binding
    /// mis-identifies the argument type.
    /// </summary>
    public static object? ConvertYamlDocumentToPSObject(KeyValuePair<YamlNode, YamlNode> pair, bool ordered = false)
        => ConvertYamlDocumentToPSObject(pair.Value, ordered);

    private static string KeyToString(YamlNode keyNode)
    {
        // PowerShell code uses $i.Value; in YAML, keys are typically scalars.
        if (keyNode is YamlScalarNode sk && sk.Value is not null)
        {
            return sk.Value;
        }

        // Fallback: ToString() so we don't throw on exotic keys.
        return keyNode.ToString() ?? string.Empty;
    }

    [GeneratedRegex(@"^[\+\-]?(?:\.?inf(?:inity)?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();

    [GeneratedRegex(@"^[\+\-]?\d+[eE][\+\-]?\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex ScientificIntRegex();

    private static BigInteger Pow10(int exp)
    {
        // Efficient power-of-ten computation without double rounding
        // Use exponentiation by squaring with base 10 as BigInteger
        var result = BigInteger.One;
        var baseVal = new BigInteger(10);
        var e = exp;
        while (e > 0)
        {
            if ((e & 1) == 1)
            {
                result *= baseVal;
            }
            baseVal *= baseVal;
            e >>= 1;
        }
        return result;
    }
}
