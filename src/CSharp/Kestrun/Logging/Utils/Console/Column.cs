namespace Kestrun.Logging.Utils.Console;

internal class Column
{
    public int Index { get; }
    public List<Cell> Cells { get; } = [];

    public int MaxWidth => Cells.Max(c => c.Width);

    public Column(int index) => Index = index;

    public Column(Cell cell)
    {
        AddCell(cell);
        Index = cell.Index;
    }

    public void AddCell(Cell cell)
    {
        Cells.Add(cell);
        cell.Column = this;
    }
}
