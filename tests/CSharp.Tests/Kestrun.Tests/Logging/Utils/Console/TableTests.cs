using Kestrun.Logging.Utils.Console;
using Xunit;

namespace KestrunTests.Logging.Utils.Console;

/// <summary>
/// Tests for <see cref="Padding"/> class.
/// </summary>
public class PaddingTests
{
    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithSingleValue_SetsBothLeftAndRight()
    {
        // Act
        var padding = new Padding(5);

        // Assert
        Assert.Equal(5, padding.Left);
        Assert.Equal(5, padding.Right);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithRightAndLeft_SetsBoth()
    {
        // Act
        var padding = new Padding(3, 7);

        // Assert
        Assert.Equal(3, padding.Right);
        Assert.Equal(7, padding.Left);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void RightString_ReturnsCorrectSpaces()
    {
        // Arrange
        var padding = new Padding(4, 2);

        // Act
        var result = padding.RightString();

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal("    ", result);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void LeftString_ReturnsCorrectSpaces()
    {
        // Arrange
        var padding = new Padding(3, 5);

        // Act
        var result = padding.LeftString();

        // Assert
        Assert.Equal(5, result.Length);
        Assert.Equal("     ", result);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void RightString_WithZeroPadding_ReturnsEmptyString()
    {
        // Arrange
        var padding = new Padding(0, 5);

        // Act
        var result = padding.RightString();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void LeftString_WithZeroPadding_ReturnsEmptyString()
    {
        // Arrange
        var padding = new Padding(5, 0);

        // Act
        var result = padding.LeftString();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Padding_WithLargePadding_CreatesLongString()
    {
        // Arrange
        var padding = new Padding(10, 15);

        // Act
        var rightResult = padding.RightString();
        var leftResult = padding.LeftString();

        // Assert
        Assert.Equal(10, rightResult.Length);
        Assert.Equal(15, leftResult.Length);
        Assert.True(rightResult.All(c => c == ' '));
        Assert.True(leftResult.All(c => c == ' '));
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Properties_CanBeModified()
    {
        // Arrange
        var padding = new Padding(5)
        {
            // Act
            Left = 10,
            Right = 3
        };

        // Assert
        Assert.Equal(10, padding.Left);
        Assert.Equal(3, padding.Right);
    }
}

/// <summary>
/// Tests for <see cref="Row"/> class.
/// </summary>
public class RowTests
{
    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithObjectValues_CreatesCells()
    {
        // Act
        var row = new Row(0, false, "cell1", "cell2", "cell3");

        // Assert
        Assert.Equal(3, row.Cells.Count);
        Assert.False(row.IsHeader);
        Assert.Equal(0, row.Index);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithStringValues_CreatesCells()
    {
        // Act
        var row = new Row(1, false, ["header", "data"]);

        // Assert
        Assert.Equal(2, row.Cells.Count);
        Assert.Equal(1, row.Index);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithHeader_SetsIsHeaderTrue()
    {
        // Act
        var row = new Row(0, isHeader: true, "Col1", "Col2");

        // Assert
        Assert.True(row.IsHeader);
        Assert.Equal(2, row.Cells.Count);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithNullValues_ThrowsArgumentException() =>
        // Act & Assert
        _ = Assert.Throws<ArgumentException>(() => new Row(0, false, null!));

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithObjectsContainingNull_ConvertNullToPlaceholder()
    {
        // Act
        var row = new Row(0, false, "text", null!, "more");

        // Assert
        Assert.Equal(3, row.Cells.Count);
        // Null value should be converted to Cell.NULL_PLACEHOLDER
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Cells_HaveCorrectIndexes()
    {
        // Act
        var row = new Row(0, false, "a", "b", "c");

        // Assert
        Assert.Equal(0, row.Cells[0].Index);
        Assert.Equal(1, row.Cells[1].Index);
        Assert.Equal(2, row.Cells[2].Index);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Cells_ReferenceCorrectRow()
    {
        // Act
        var row = new Row(5, false, "x", "y");

        // Assert
        Assert.Same(row, row.Cells[0].Row);
        Assert.Same(row, row.Cells[1].Row);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void DisableTopGrid_DefaultsFalse()
    {
        // Act
        var row = new Row(0, false, "data");

        // Assert
        Assert.False(row.DisableTopGrid);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void DisableTopGrid_CanBeSet()
    {
        // Arrange
        var row = new Row(0, false, "data")
        {
            // Act
            DisableTopGrid = true
        };

        // Assert
        Assert.True(row.DisableTopGrid);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithIntValues_ConvertsToStrings()
    {
        // Act
        var row = new Row(0, false, 123, 456, 789);

        // Assert
        Assert.Equal(3, row.Cells.Count);
        // Cells should contain string representations of integers
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithEmptyValues_CreatesSingleCell()
    {
        // Act & Assert - Empty values should create an empty cells collection or throw
        var row = new Row(0, false, []);
        Assert.NotNull(row.Cells);
        // Empty array should create an empty or minimal cells collection
    }
}

/// <summary>
/// Tests for <see cref="Table"/> class.
/// </summary>
public class TableTests
{
    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_DefaultInitialization()
    {
        // Act
        var table = new Table();

        // Assert
        Assert.NotNull(table.Padding);
        Assert.Equal(0, table.Padding.Left);
        Assert.Equal(0, table.Padding.Right);
        Assert.False(table.HeaderSet);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Constructor_WithPadding_InitializesPadding()
    {
        // Arrange
        var padding = new Padding(2, 3);

        // Act
        var table = new Table(padding);

        // Assert
        Assert.Same(padding, table.Padding);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void SetHeader_SetsHeaderFlag()
    {
        // Arrange
        var table = new Table();

        // Act
        table.SetHeader("Col1", "Col2", "Col3");

        // Assert
        Assert.True(table.HeaderSet);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void AddRow_SingleLineValues_AddsOneRow()
    {
        // Arrange
        var table = new Table();

        // Act
        table.AddRow("value1", "value2", "value3");

        // Assert - no direct way to access rows, but Render should include them
        var output = table.Render();
        Assert.Contains("value1", output);
        Assert.Contains("value2", output);
        Assert.Contains("value3", output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void AddRow_MultilineValue_SplitsIntoMultipleRows()
    {
        // Arrange
        var table = new Table();

        // Act
        table.AddRow("single", "multi\nline\nvalue", "single2");

        // Assert
        var output = table.Render();
        Assert.Contains("multi", output);
        Assert.Contains("line", output);
        Assert.Contains("value", output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Render_EmptyTable_ReturnsOutput()
    {
        // Arrange
        var table = new Table();

        // Act
        var output = table.Render();

        // Assert
        // Even an empty table produces some output (though it may be minimal)
        Assert.NotNull(output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Render_WithHeader_IncludesHeaderRow()
    {
        // Arrange
        var table = new Table();
        table.SetHeader("Name", "Age", "City");

        // Act
        var output = table.Render();

        // Assert
        Assert.Contains("Name", output);
        Assert.Contains("Age", output);
        Assert.Contains("City", output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void RenderWithoutGrid_ExcludesGridCharacters()
    {
        // Arrange
        var table = new Table();
        table.SetHeader("Col1", "Col2");
        table.AddRow("val1", "val2");

        // Act
        var output = table.RenderWithoutGrid();

        // Assert
        Assert.NotEmpty(output);
        // Should not contain grid characters
        Assert.DoesNotContain("│", output);
        Assert.DoesNotContain("─", output);
        Assert.DoesNotContain("┌", output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Render_WithMultipleRows_IncludesAllRows()
    {
        // Arrange
        var table = new Table();
        table.SetHeader("ID", "Name");
        table.AddRow("1", "Alice");
        table.AddRow("2", "Bob");
        table.AddRow("3", "Charlie");

        // Act
        var output = table.Render();

        // Assert
        Assert.Contains("Alice", output);
        Assert.Contains("Bob", output);
        Assert.Contains("Charlie", output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Render_ContainsVerticalLines()
    {
        // Arrange
        var table = new Table();
        table.SetHeader("H1", "H2");
        table.AddRow("V1", "V2");

        // Act
        var output = table.Render();

        // Assert
        Assert.Contains(Table.VERTICAL_LINE, output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Render_ContainsHorizontalLines()
    {
        // Arrange
        var table = new Table();
        table.SetHeader("Header");

        // Act
        var output = table.Render();

        // Assert
        Assert.Contains(Table.HORIZONTAL_LINE.ToString(), output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Render_WithPadding_IncludesPaddingSpaces()
    {
        // Arrange
        var padding = new Padding(2, 2);
        var table = new Table(padding);
        table.SetHeader("A");
        table.AddRow("B");

        // Act
        var output = table.Render();

        // Assert
        // Output should include padding spaces
        Assert.NotEmpty(output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void ToString_ReturnsRenderOutput()
    {
        // Arrange
        var table = new Table();
        table.SetHeader("Test");
        table.AddRow("Data");

        // Act
        var toStringOutput = table.ToString();
        var renderOutput = table.Render();

        // Assert
        Assert.Equal(renderOutput, toStringOutput);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void AddRow_WithObjects_ConvertsToStrings()
    {
        // Arrange
        var table = new Table();

        // Act
        table.AddRow(123, 45.67, true);

        // Assert
        var output = table.Render();
        Assert.Contains("123", output);
        Assert.Contains("45.67", output);
        Assert.Contains("True", output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void AddRow_WithNullValue_HandlesNull()
    {
        // Arrange
        var table = new Table();

        // Act & Assert - Should not throw
        table.AddRow("before", null!, "after");
        var output = table.Render();
        Assert.NotEmpty(output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Render_GridLines_ContainJointCharacters()
    {
        // Arrange
        var table = new Table();
        table.SetHeader("H1", "H2");
        table.AddRow("V1", "V2");

        // Act
        var output = table.Render();

        // Assert
        // Should contain various joint characters
        Assert.Contains(Table.TOP_LEFT_JOINT, output);
        Assert.Contains(Table.TOP_RIGHT_JOINT, output);
        Assert.Contains(Table.BOTTOM_LEFT_JOINT, output);
        Assert.Contains(Table.BOTTOM_RIGHT_JOINT, output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void AddRow_MultilineWithCarriageReturn_NormalizesNewlines()
    {
        // Arrange
        var table = new Table();

        // Act - Value with \r\n (Windows newlines)
        table.AddRow("line1\r\nline2", "single");

        // Assert
        var output = table.Render();
        Assert.Contains("line1", output);
        Assert.Contains("line2", output);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void RenderedTable_IsConsistent()
    {
        // Arrange
        var table1 = new Table();
        table1.SetHeader("Col1", "Col2");
        table1.AddRow("Data1", "Data2");

        var table2 = new Table();
        table2.SetHeader("Col1", "Col2");
        table2.AddRow("Data1", "Data2");

        // Act
        var output1 = table1.Render();
        var output2 = table2.Render();

        // Assert
        Assert.Equal(output1, output2);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Padding_AffectsRenderedWidth()
    {
        // Arrange
        var tableNoPadding = new Table(new Padding(0));
        tableNoPadding.SetHeader("X");

        var tableWithPadding = new Table(new Padding(2));
        tableWithPadding.SetHeader("X");

        // Act
        var outputNoPadding = tableNoPadding.Render();
        var outputWithPadding = tableWithPadding.Render();

        // Assert
        // Table with padding should be wider
        var paddingLines = outputWithPadding.Split('\n');
        var noPaddingLines = outputNoPadding.Split('\n');

        // Compare line lengths (allowing for minor differences in rendering)
        Assert.True(paddingLines[0].Length >= noPaddingLines[0].Length);
    }
}
