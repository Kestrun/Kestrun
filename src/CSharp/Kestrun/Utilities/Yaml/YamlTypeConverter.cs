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

        var tag = scalar.Tag.Value;           // e.g., "tag:yaml.org,2002:int"
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
                    // Round-trip parsing; prefer DateTimeOffset then fold to DateTime (UTC) for parity
                    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                                                DateTimeStyles.RoundtripKind, out var dto))
                    {
                        return dto.UtcDateTime;
                    }

                    if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                          DateTimeStyles.RoundtripKind, out var dt))
                    {
                        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
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
    /// Convert a YamlSequenceNode to an array (object[]), preserving element order.
    /// </summary>
    public static object[] ConvertYamlSequenceToArray(YamlSequenceNode node, bool ordered = false)
    {
        var list = new List<object?>(node.Children.Count);
        foreach (var child in node.Children)
        {
            list.Add(ConvertYamlDocumentToPSObject(child, ordered));
        }
        return list.ToArray();
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
            YamlScalarNode _ => ConvertValueToProperType(node),
            _ => node // fallback: return the node itself
        };

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
}
