
namespace Kestrun.Logging.Utils.Console.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="Table"/> class.
/// </summary>
public static class TableExtensions
{
    /// <summary>
    /// Adds one row with two cells into <see cref="Table"/> only if <paramref name="propertyValue"/> is not null or empty string.
    /// </summary>
    /// <param name="table"></param>
    /// <param name="propertyName"></param>
    /// <param name="propertyValue"></param>
    public static void AddPropertyRow(this Table table, string propertyName, object propertyValue)
    {
        if (propertyValue == null || (propertyValue is string propertyValueString && string.IsNullOrEmpty(propertyValueString)))
        {
            return;
        }

        // Avoid calling ToString() on non-string values here; Table and Cell will defer formatting.
        // Clamp extremely long strings to keep rendering safe and bounded.
        if (propertyValue is string s)
        {
            const int maxLen = 8_192; // safety cap
            var safe = s.Length > maxLen ? s[..maxLen] + "…" : s;
            table.AddRow(propertyName, safe);
        }
        else
        {
            table.AddRow(propertyName, propertyValue);
        }
    }
}
