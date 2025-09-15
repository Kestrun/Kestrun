namespace Kestrun.Logging.Utils.Console;

internal class Row
{
    public bool IsHeader { get; }
    public int Index { get; }
    public List<Cell> Cells { get; }
    public bool DisableTopGrid { get; set; }

    public Row(int index, bool isHeader = false, params object[] values)
        : this(index, isHeader, [.. (values ?? []).Select(v => v?.ToString() ?? Cell.NULL_PLACEHOLDER)])
    {
    }

    public Row(int index, bool isHeader = false, params string[] values)
    {
        if (values == null)
        {
            throw new ArgumentException("You must provide cells when creating row!", nameof(values));
        }

        var cellIndex = 0;
        Cells = [.. values.Select(v => new Cell(v, cellIndex++, this))];
        Index = index;
        IsHeader = isHeader;
    }
}
