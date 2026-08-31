using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StageMaker.Models;
using StageMaker.Services;

namespace StageMaker.Controls;

/// <summary>
/// Shared behavior for a clickable cell that opens a dropdown grid of every block
/// image under Blocks/ and applies the picked block to itself. Used by both the
/// 9x9 stage grid cells (<see cref="StageCell"/>) and the random-fill pool cells
/// (<see cref="PoolSlot"/>).
/// </summary>
public abstract class BlockPickerCell : Border
{
    private const int ThumbnailPixelWidth = 64;
    private const int DropdownColumns = 4;

    private static readonly Dictionary<string, BitmapImage> ThumbnailCache = new();

    private readonly Image _image;
    private readonly TextBlock _placeholder;
    private readonly Popup _popup;
    private bool _popupContentBuilt;

    public BlockDefinition? SelectedBlock { get; private set; }

    public event EventHandler? SelectedBlockChanged;

    protected BlockPickerCell()
    {
        BorderBrush = Brushes.DimGray;
        BorderThickness = new Thickness(0.5);
        Background = Brushes.Transparent;
        Cursor = Cursors.Hand;
        ToolTip = "클릭하여 블록 선택";

        _placeholder = new TextBlock
        {
            Text = "+",
            Foreground = Brushes.Gray,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _image = new Image
        {
            Stretch = Stretch.Uniform,
            Margin = new Thickness(3),
            Visibility = Visibility.Collapsed
        };

        var content = new Grid();
        content.Children.Add(_placeholder);
        content.Children.Add(_image);
        Child = content;

        _popup = new Popup
        {
            PlacementTarget = this,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true
        };

        MouseLeftButtonUp += (_, _) => OpenDropdown();
    }

    /// <summary>Sets the initial block without raising SelectedBlockChanged (used for defaults).</summary>
    protected void SetInitialSelection(BlockDefinition block) => ApplySelection(block, raiseEvent: false);

    /// <summary>Programmatically applies a block selection, e.g. from a random-fill action.</summary>
    public void SetBlock(BlockDefinition block, bool raiseEvent = true) => ApplySelection(block, raiseEvent);

    protected virtual string DescribeSelection(BlockDefinition block) => $"{block.Id} ({block.FileName})";

    private void OpenDropdown()
    {
        if (!_popupContentBuilt)
        {
            _popup.Child = BuildDropdownContent();
            _popupContentBuilt = true;
        }

        _popup.IsOpen = true;
    }

    private UIElement BuildDropdownContent()
    {
        var itemsPanel = new WrapPanel
        {
            Width = DropdownColumns * 64
        };

        foreach (var block in BlockCatalog.All)
        {
            itemsPanel.Children.Add(BuildCatalogItem(block));
        }

        var scrollViewer = new ScrollViewer
        {
            Content = itemsPanel,
            Width = itemsPanel.Width,
            MaxHeight = 360,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25)),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            Child = scrollViewer
        };
    }

    private Border BuildCatalogItem(BlockDefinition block)
    {
        var image = new Image
        {
            Source = GetThumbnail(block),
            Width = 48,
            Height = 48,
            Stretch = Stretch.Uniform
        };

        var label = new TextBlock
        {
            Text = block.Id,
            Foreground = Brushes.White,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var stack = new StackPanel { Margin = new Thickness(2) };
        stack.Children.Add(image);
        stack.Children.Add(label);

        var item = new Border
        {
            Width = 60,
            Height = 68,
            Margin = new Thickness(2),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = block.FileName,
            Child = stack
        };

        item.MouseEnter += (_, _) => item.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x45, 0x45));
        item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
        item.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            ApplySelection(block, raiseEvent: true);
            _popup.IsOpen = false;
        };

        return item;
    }

    private void ApplySelection(BlockDefinition block, bool raiseEvent)
    {
        SelectedBlock = block;
        _image.Source = GetThumbnail(block);
        _image.Visibility = Visibility.Visible;
        _placeholder.Visibility = Visibility.Collapsed;
        ToolTip = DescribeSelection(block);

        if (raiseEvent)
        {
            SelectedBlockChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static BitmapImage GetThumbnail(BlockDefinition block)
    {
        if (ThumbnailCache.TryGetValue(block.FullPath, out var cached))
        {
            return cached;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(block.FullPath);
        bitmap.DecodePixelWidth = ThumbnailPixelWidth;
        bitmap.EndInit();
        bitmap.Freeze();

        ThumbnailCache[block.FullPath] = bitmap;
        return bitmap;
    }
}
