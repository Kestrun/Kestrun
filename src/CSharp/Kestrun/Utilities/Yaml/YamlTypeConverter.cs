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
    // Integers (promote via BigInteger, then downcast if safe)
    // Uses cached BigInteger boundary constants (declare once at class scope):
    private static readonly BigInteger IntMinBig = new(int.MinValue);
    private static readonly BigInteger IntMaxBig = new(int.MaxValue);
    private static readonly BigInteger LongMinBig = new(long.MinValue);
    private static readonly BigInteger LongMaxBig = new(long.MaxValue);

    /// <summary>
    /// Convert a YamlNode to the most appropriate .NET type based on its tag and content
    /// </summary>
    /// <param name="node">The YAML node to convert.</param>
    /// <returns>The converted .NET object, or null if the node is null.</returns>
    /// <exception cref="FormatException">Thrown when the node's value cannot be converted to the appropriate type.</exception>
    public static object? ConvertValueToProperType(YamlNode node)
    {
        if (node is not YamlScalarNode scalar)
        {
            return node; // non-scalar passthrough
        }

        var tag = GetSafeTagValue(scalar);
        var value = scalar.Value;
        if (value is null)
        {
            return scalar; // null scalar value
        }

        if (!string.IsNullOrEmpty(tag))
        {
            var tagged = TryParseTaggedScalar(tag, value);
            if (tagged.parsed)
            {
                return tagged.value;
            }
        }

        if (scalar.Style == ScalarStyle.Plain)
        {
            var plain = TryParsePlainScalar(value);
            if (plain.parsed)
            {
                return plain.value;
            }

            if (IsExplicitNullToken(value))
            {
                return null;
            }
        }

        return value; // fallback string
    }

    /// <summary>Safely obtains a scalar tag string or null when non-specific/unavailable.</summary>
    private static string? GetSafeTagValue(YamlScalarNode scalar)
    {
        try
        {
            if (!scalar.Tag.IsEmpty && !scalar.Tag.IsNonSpecific)
            {
                return scalar.Tag.Value;
            }
        }
        catch
        {
            // ignore and return null
        }
        return null;
    }

    /// <summary>Attempts to parse a tagged scalar. Returns (parsed=false) if tag unrecognized.</summary>
    private static (bool parsed, object? value) TryParseTaggedScalar(string tag, string value)
    {
        switch (tag)
        {
            case "tag:yaml.org,2002:str":
                return (true, value);
            case "tag:yaml.org,2002:null":
                return (true, null);
            case "tag:yaml.org,2002:bool":
                if (bool.TryParse(value, out var b))
                {
                    return (true, b);
                }
                throw new FormatException($"Failed to parse scalar '{value}' as boolean.");
            case "tag:yaml.org,2002:int":
                return (true, ParseYamlInt(value));
            case "tag:yaml.org,2002:float":
                return (true, ParseTaggedFloat(value));
            case "tag:yaml.org,2002:timestamp":
                return (true, ParseTaggedTimestamp(value));
            default:
                return (false, null);
        }
    }

    /// <summary>Parses a YAML float (tagged) honoring infinity tokens, else decimal.</summary>
    private static object ParseTaggedFloat(string value)
    {
        if (InfinityRegex.IsMatch(value))
        {
            return value.StartsWith('-') ? double.NegativeInfinity : double.PositiveInfinity;
        }
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec))
        {
            return dec;
        }
        throw new FormatException($"Failed to parse scalar '{value}' as decimal.");
    }

    /// <summary>Parses a YAML timestamp preserving unspecified semantics or converting to local DateTime for zone-aware values.</summary>
    private static object ParseTaggedTimestamp(string value)
    {
        var hasTime = value.Contains(':');
        var hasExplicitZone = hasTime && (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase) || MyRegex1().IsMatch(value));
        if (hasExplicitZone && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            return dto.LocalDateTime;
        }
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var naive))
        {
            return DateTime.SpecifyKind(naive, DateTimeKind.Unspecified);
        }
        throw new FormatException($"Failed to parse scalar '{value}' as DateTime.");
    }

    /// <summary>Attempts plain-style heuristic parsing for bool/int/float. Returns (parsed=false) if none match.</summary>
    private static (bool parsed, object? value) TryParsePlainScalar(string value)
    {
        if (bool.TryParse(value, out var bPlain))
        {
            return (true, bPlain);
        }
        if (TryParseBigInteger(value, out var bigInt))
        {
#pragma warning disable IDE0078
            if (bigInt >= IntMinBig && bigInt <= IntMaxBig)
            {
                return (true, (int)bigInt);
            }
            if (bigInt >= LongMinBig && bigInt <= LongMaxBig)
            {
                return (true, (long)bigInt);
            }
#pragma warning restore IDE0078
            return (true, bigInt);
        }
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dPlain))
        {
            return (true, dPlain);
        }
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblPlain))
        {
            return (true, dblPlain);
        }
        return (false, null);
    }

    /// <summary>Determines if a plain scalar is an explicit YAML null token.</summary>
    private static bool IsExplicitNullToken(string value)
        => value == string.Empty || value == "~" || value == "$null" || value == "''" ||
           string.Equals(value, "null", StringComparison.Ordinal) ||
           string.Equals(value, "Null", StringComparison.Ordinal) ||
           string.Equals(value, "NULL", StringComparison.Ordinal);

    /// <summary>
    /// Parse a YAML integer scalar, handling base prefixes (0o, 0x) and scientific notation (e.g., 1e3).
    /// </summary>
    /// <param name="value">The YAML integer scalar to parse.</param>
    /// <returns>The parsed integer value.</returns>
    /// <exception cref="FormatException">Thrown when the input is not a valid YAML integer.</exception>
    private static object ParseYamlInt(string value)
    {
        // 1. Scientific notation branch
        if (ScientificIntRegex().IsMatch(value))
        {
            return ParseScientificInteger(value);
        }

        // 2. Base-prefixed branch (octal / hex)
        if (TryParseBasePrefixedInteger(value, out var basePrefixed))
        {
            return basePrefixed;
        }

        // 3. Generic integer (BigInteger + downcast)
        return ParseGenericInteger(value);
    }

    /// <summary>
    /// Parse a scientific-notation integer (mantissa * 10^exp) ensuring exponent is non-negative and result integral.
    /// Mirrors previous behavior including exception wrapping.
    /// </summary>
    private static object ParseScientificInteger(string value)
    {
        try
        {
            var (mantissa, sign, exp) = SplitScientificParts(value);
            if (exp < 0)
            {
                throw new FormatException($"Failed to parse scalar '{value}' as integer (negative exponent not integral).");
            }
            var pow = Pow10(exp);
            var bigVal = mantissa * pow * sign;
            return DowncastBigInteger(bigVal);
        }
        catch (Exception ex)
        {
            throw new FormatException($"Failed to parse scalar '{value}' as integer (scientific).", ex);
        }
    }

    /// <summary>
    /// Splits a scientific notation string into mantissa BigInteger, sign (+/-1), and exponent.
    /// </summary>
    /// <param name="value">The scientific notation string (e.g., "1.23e+3").</param>
    /// <returns>A tuple containing the mantissa as BigInteger, the sign as int, and the exponent as int.</returns>
    private static (BigInteger mantissa, int sign, int exp) SplitScientificParts(string value)
    {
        var parts = value.Split('e', 'E');
        var mantissaStr = parts[0];
        var expStr = parts[1];
        var exp = int.Parse(expStr, CultureInfo.InvariantCulture);
        var sign = 1;
        if (mantissaStr.StartsWith('+'))
        {
            mantissaStr = mantissaStr[1..];
        }
        else if (mantissaStr.StartsWith('-'))
        {
            sign = -1;
            mantissaStr = mantissaStr[1..];
        }
        if (mantissaStr.Length > 1 && mantissaStr.All(c => c == '0'))
        {
            mantissaStr = "0"; // normalize all-zero mantissa
        }
        return !BigInteger.TryParse(mantissaStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mantissa)
            ? throw new FormatException($"Failed to parse scalar '{value}' as integer (mantissa invalid).")
            : (mantissa, sign, exp);
    }

    /// <summary>
    /// Attempts to parse octal (0o) or hexadecimal (0x) integer representations, returning true if handled.
    /// </summary>
    /// <param name="value">The input string to parse.</param>
    /// <param name="result">The parsed integer value.</param>
    /// <returns>True if the input was successfully parsed as a base-prefixed integer; otherwise, false.</returns>
    private static bool TryParseBasePrefixedInteger(string value, out object result)
    {
        result = default!;
        if (value.Length <= 2)
        {
            return false;
        }
        var prefix = value[..2];
        if (prefix.Equals("0o", StringComparison.OrdinalIgnoreCase))
        {
            var asLong = Convert.ToInt64(value[2..], 8);
            result = DowncastInteger(asLong);
            return true;
        }
        if (prefix.Equals("0x", StringComparison.OrdinalIgnoreCase))
        {
            var asLong = Convert.ToInt64(value[2..], 16);
            result = DowncastInteger(asLong);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Generic integer parsing path using BigInteger with downcasting. Throws FormatException on failure.
    /// </summary>
    /// <param name="value">The input string to parse.</param>
    /// <returns>The parsed integer value.</returns>
    /// <exception cref="FormatException">Thrown when the input is not a valid integer.</exception>
    private static object ParseGenericInteger(string value)
    {
        return !TryParseBigInteger(value, out var big)
            ? throw new FormatException($"Failed to parse scalar '{value}' as integer.")
            : DowncastBigInteger(big);
    }

    /// <summary>
    /// Downcasts a BigInteger to int or long when within range; otherwise returns the original BigInteger.
    /// </summary>
    /// <param name="bigVal">The BigInteger value to downcast.</param>
    /// <returns>The downcasted integer value or the original BigInteger if out of range.</returns>
    private static object DowncastBigInteger(BigInteger bigVal)
    {
#pragma warning disable IDE0078
        if (bigVal >= int.MinValue && bigVal <= int.MaxValue)
        {
            return (int)bigVal;
        }
        if (bigVal >= long.MinValue && bigVal <= long.MaxValue)
        {
            return (long)bigVal;
        }
#pragma warning restore IDE0078
        return bigVal;
    }

    /// <summary>
    /// Attempts to parse a string as a BigInteger, including support for scientific notation with non-negative exponents.
    /// </summary>
    /// <param name="s">The input string to parse.</param>
    /// <param name="result">The parsed BigInteger value.</param>
    /// <returns>True if the parsing was successful; otherwise, false.</returns>
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
                if (mantissaStr.StartsWith('+'))
                {
                    mantissaStr = mantissaStr[1..];
                }
                else if (mantissaStr.StartsWith('-'))
                {
                    sign = -1;
                    mantissaStr = mantissaStr[1..];
                }
                if (!BigInteger.TryParse(mantissaStr, NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var mantissa))
                {
                    result = default;
                    return false;
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
            NumberStyles.Integer | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out result
        );
    }

    private static object DowncastInteger(long v) => v is >= int.MinValue and <= int.MaxValue ? (int)v : (object)v;

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
                var val = key == "datesAsStrings" && kv.Value is YamlSequenceNode seq
                    ? seq.Children.Select(c => c is YamlScalarNode s ? s.Value : c.ToString()).ToArray()
                    : ConvertYamlDocumentToPSObject(kv.Value, ordered);
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
                ret[key] = key == "datesAsStrings" && kv.Value is YamlSequenceNode seq
                    ? seq.Children.Select(c => c is YamlScalarNode s ? s.Value : c.ToString()).ToArray()
                    : ConvertYamlDocumentToPSObject(kv.Value, ordered);
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

    [GeneratedRegex(@"[\+\-]\d{1,2}(:?\d{2})?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex1();
}
