using System.Windows;
using System.Windows.Media;
using StageMaker.Models;

namespace StageMaker.Controls;

/// <summary>
/// One slot of the random-fill pool: the set of blocks used to fill any stage
/// cell left empty when exporting. Same dropdown picker as <see cref="StageCell"/>,
/// styled as a fixed-size palette cell and optionally pre-selected with a default block.
/// </summary>
public sealed class PoolSlot : BlockPickerCell
{
    private const int SlotSize = 56;

    public PoolSlot(BlockDefinition? defaultBlock = null)
    {
        Width = SlotSize;
        Height = SlotSize;
        Margin = new Thickness(4);
        Background = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C));

        if (defaultBlock is not null)
        {
            SetInitialSelection(defaultBlock);
        }
    }
}
