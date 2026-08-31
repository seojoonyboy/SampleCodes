using StageMaker.Models;

namespace StageMaker.Controls;

/// <summary>
/// One cell of the 9x9 stage grid. Click opens a dropdown grid of every block image
/// found under Blocks/; picking one places that block's image directly in this cell.
/// </summary>
public sealed class StageCell : BlockPickerCell
{
    public int Row { get; }
    public int Col { get; }

    public StageCell(int row, int col)
    {
        Row = row;
        Col = col;
    }

    protected override string DescribeSelection(BlockDefinition block) => $"({Row}, {Col}) - {block.Id} ({block.FileName})";
}
