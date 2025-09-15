using System.Text;

namespace Kestrun.Logging.Utils.Console;

/// <summary>
/// Represents a console table for formatted output with optional grid and padding.
/// </summary>
public class Table
{
    /// <summary>
    /// Represents the top-left joint character for the table grid.
    /// </summary>
    public const string TOP_LEFT_JOINT = "┌";
    /// <summary>
    /// Represents the top-right joint character for the table grid.
    /// </summary>
    public const string TOP_RIGHT_JOINT = "┐";
    /// <summary>
    /// Represents the bottom-left joint character for the table grid.
    /// </summary>
    public const string BOTTOM_LEFT_JOINT = "└";
    /// <summary>
    /// Represents the bottom-right joint character for the table grid.
    /// </summary>
    public const string BOTTOM_RIGHT_JOINT = "┘";
    /// <summary>
    /// Represents the top joint character for the table grid.
    /// </summary>
    public const string TOP_JOINT = "┬";
    /// <summary>
    /// Represents the bottom joint character for the table grid.
    /// </summary>
    public const string BOTTOM_JOINT = "┴";
    /// <summary>
    /// Represents the left joint character for the table grid.
    /// </summary>
    public const string LEFT_JOINT = "├";
    /// <summary>
    /// Represents the middle joint character for the table grid.
    /// </summary>
    public const string MIDDLE_JOINT = "┼";
    /// <summary>
    /// Represents the right joint character for the table grid.
    /// </summary>
    public const string RIGHT_JOINT = "┤";
    /// <summary>
    /// Represents the horizontal line character for the table grid.
    /// </summary>
    public const char HORIZONTAL_LINE = '─';
    /// <summary>
    /// Represents the vertical line character for the table grid.
    /// </summary>
    public const string VERTICAL_LINE = "│";

    private List<Row> Rows { get; } = [];
    private List<Column> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the table header has been set.
    /// </summary>
    public bool HeaderSet { get; set; }
    /// <summary>
    /// Gets the padding configuration for the table cells.
    /// </summary>
    public Padding Padding { get; } = new Padding(0);

    private int _rowIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="Table"/> class with default padding.
    /// </summary>
    public Table() => Columns = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Table"/> class with the specified padding.
    /// </summary>
    /// <param name="padding">The padding configuration for the table cells.</param>
    public Table(Padding padding)
    {
        Padding = padding;
        Columns = [];
    }

    /// <summary>
    /// Sets the header row for the table using the specified header values.
    /// </summary>
    /// <param name="headers">An array of header strings to be used as the table's header row.</param>
    public void SetHeader(params string[] headers)
    {
        HeaderSet = true;
        Rows.Add(new Row(_rowIndex++, true, headers));
    }

    /// <summary>
    /// Adds a row to the table with the specified cell values.
    /// Handles multiline cell values by splitting them into multiple rows.
    /// </summary>
    /// <param name="values">An array of objects representing the cell values for the row.</param>
    public void AddRow(params object[] values)
    {
        // Only consider string values for multiline handling to avoid heavy ToString() on complex objects
        if (values.Any(v => v is string s && s.Contains(Environment.NewLine)))
        {
            var maxLines = values.Max(v => v is string s ? s.Split('\n').Length : 1);
            for (var i = 0; i < maxLines; i++)
            {
                var row = new List<object>();
                foreach (var value in values)
                {
                    if (value is string s)
                    {
                        var valueLines = s.Split('\n');
                        row.Add(i < valueLines.Length ? valueLines[i].Replace("\r", "") : string.Empty);
                    }
                    else
                    {
                        // Non-strings are treated as single-line values
                        row.Add(i == 0 ? value : string.Empty);
                    }
                }

                Rows.Add(new Row(_rowIndex++, false, row.ToArray()) { DisableTopGrid = i > 0 });
            }
        }
        else
        {
            // Add a single row (fixes previous accidental recursion)
            Rows.Add(new Row(_rowIndex++, false, values));
        }
    }

    private void CalculateColumns()
    {
        Columns = [];
        foreach (var row in Rows)
        {
            foreach (var cell in row.Cells)
            {
                if (row.Index == 0)
                {
                    Columns.Add(new Column(cell));
                }
                else
                {
                    Columns.SingleOrDefault(c => c.Index == cell.Index)?.AddCell(cell);
                }
            }
        }
    }

    /// <summary>
    /// Renders the table as a formatted string with grid lines.
    /// </summary>
    /// <returns>A string representing the formatted table with grid.</returns>
    public string Render()
    {
        var sb = new StringBuilder();
        CalculateColumns();

        foreach (var row in Rows)
        {
            // Don't render grid if row is multiline
            if (!row.DisableTopGrid)
            {
                RenderGrid(sb, row.Index);
            }

            foreach (var cell in row.Cells)
            {
                _ = sb.Append($"{VERTICAL_LINE}{Padding.LeftString()}{cell}{Padding.RightString()}");
            }
            _ = sb.AppendLine(VERTICAL_LINE);

            RenderGrid(sb, row.Index, false);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Renders the table as a formatted string without grid lines.
    /// </summary>
    /// <returns>A string representing the formatted table without grid.</returns>
    public string RenderWithoutGrid()
    {
        var sb = new StringBuilder();
        CalculateColumns();

        foreach (var row in Rows)
        {
            foreach (var cell in row.Cells)
            {
                _ = sb.Append($"{Padding.LeftString()}{cell}{Padding.RightString()}");
            }
            _ = sb.AppendLine();
        }

        return sb.ToString();
    }

    private void RenderGrid(StringBuilder sb, int rowIndex, bool preRender = true)
    {
        if (rowIndex == 0 && preRender) // First line
        {
            RenderGridLine(sb, TOP_LEFT_JOINT, TOP_JOINT, TOP_RIGHT_JOINT);
        }
        else if (rowIndex == Rows.Count - 1 && !preRender)  // Last line
        {
            RenderGridLine(sb, BOTTOM_LEFT_JOINT, BOTTOM_JOINT, BOTTOM_RIGHT_JOINT);
        }
        else if (preRender) // Middle line
        {
            RenderGridLine(sb, LEFT_JOINT, MIDDLE_JOINT, RIGHT_JOINT);
        }
    }

    private void RenderGridLine(StringBuilder sb, string leftJoint, string middleJoint, string rightJoint)
    {
        for (var i = 0; i < Columns.Count; i++)
        {
            var columnWidth = Columns[i].MaxWidth + Padding.Right + Padding.Left;
            _ = i == 0
                ? sb.Append(leftJoint + string.Empty.PadLeft(columnWidth, HORIZONTAL_LINE) + middleJoint)
                : i == Columns.Count - 1
                    ? sb.Append(string.Empty.PadLeft(columnWidth, HORIZONTAL_LINE) + rightJoint)
                    : sb.Append(string.Empty.PadLeft(columnWidth, HORIZONTAL_LINE) + middleJoint);
        }
        _ = sb.AppendLine();
    }

    /// <summary>
    /// Returns a string that represents the current table.
    /// </summary>
    /// <returns>A string representation of the table.</returns>
    public override string ToString() => Render();
}
