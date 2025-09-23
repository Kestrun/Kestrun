using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace Kestrun.Utilities.Yaml;

/// <summary>
/// YAML emitter that quotes strings that might be misinterpreted as other types
/// </summary>
/// <param name="next">The next event emitter in the chain</param>
public partial class StringQuotingEmitter(IEventEmitter next) : ChainedEventEmitter(next)
{
    // Patterns from https://yaml.org/spec/1.2/spec.html#id2804356
    private static readonly Regex quotedRegex = MyRegex();

    /// <summary>
    /// Emit a scalar event, quoting strings that might be misinterpreted as other types
    /// </summary>
    /// <param name="eventInfo">The event information</param>
    /// <param name="emitter">The YAML emitter</param>
    public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
    {
        var typeCode = eventInfo.Source.Value != null
        ? Type.GetTypeCode(eventInfo.Source.Type)
        : TypeCode.Empty;

        switch (typeCode)
        {
            case TypeCode.Char:
                if (eventInfo.Source.Value is char c && char.IsDigit(c))
                {
                    eventInfo.Style = ScalarStyle.DoubleQuoted;
                }
                break;
            case TypeCode.String:
                var val = eventInfo.Source.Value?.ToString() ?? string.Empty;
                // Do NOT quote empty string; leave it blank so downstream null detection (blank value) works.
                if (val.Length > 0 && quotedRegex.IsMatch(val))
                {
                    eventInfo.Style = ScalarStyle.DoubleQuoted;
                }
                else if (val.IndexOf('\n') > -1)
                {
                    eventInfo.Style = ScalarStyle.Literal;
                }
                break;
        }

        base.Emit(eventInfo, emitter);
    }

    [GeneratedRegex(@"^(\~|null|true|false|on|off|yes|no|y|n|[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?|[-+]?(\.inf))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}
