using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MonitorBrightness;

public sealed class MonitorUi
{
    public required MonitorDevice Monitor { get; init; }
    public required Border Card { get; init; }
    public Slider? Slider { get; init; }
    public TextBlock? PercentLabel { get; init; }
}

internal sealed class MonitorCardFactory
{
    private readonly BrightnessUpdateQueue _brightnessUpdates;
    private readonly Action<int> _onMonitorSelected;

    public MonitorCardFactory(BrightnessUpdateQueue brightnessUpdates, Action<int> onMonitorSelected)
    {
        _brightnessUpdates = brightnessUpdates;
        _onMonitorSelected = onMonitorSelected;
    }

    public (UIElement Element, MonitorUi Control) Create(MonitorDevice monitor)
    {
        var card = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10),
        };
        card.PointerPressed += (_, _) => _onMonitorSelected(monitor.Index);

        var stack = new StackPanel { Spacing = 4 };
        Slider? slider = null;
        TextBlock? pctText = null;

        stack.Children.Add(CreateTopRow(monitor));

        if (!monitor.SupportsBrightness)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "⚠ DDC/CI not supported",
                Opacity = 0.6,
                FontSize = 11,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
        else
        {
            (slider, pctText) = CreateBrightnessRow(monitor);
            stack.Children.Add(CreateSliderGrid(slider, pctText));
        }

        card.Child = stack;

        var control = new MonitorUi
        {
            Monitor = monitor,
            Card = card,
            Slider = slider,
            PercentLabel = pctText,
        };

        return (card, control);
    }

    private static UIElement CreateTopRow(MonitorDevice monitor)
    {
        var topRow = new Grid();
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 55, 90, 180)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 1, 6, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock
            {
                Text = (monitor.Index + 1).ToString(),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
            }
        };
        Grid.SetColumn(badge, 0);
        topRow.Children.Add(badge);

        var nameBlock = new TextBlock
        {
            Text = monitor.DisplayName,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(nameBlock, 1);
        topRow.Children.Add(nameBlock);

        var detailsBlock = new TextBlock
        {
            Text = monitor.Resolution,
            Opacity = 0.5,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        Grid.SetColumn(detailsBlock, 2);
        topRow.Children.Add(detailsBlock);

        return topRow;
    }

    private (Slider slider, TextBlock pctText) CreateBrightnessRow(MonitorDevice monitor)
    {
        var slider = new Slider
        {
            Minimum = monitor.MinBrightness,
            Maximum = monitor.MaxBrightness,
            Value = monitor.CurrentBrightness,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 32,
        };

        var pctText = new TextBlock
        {
            Text = $"{monitor.CurrentBrightness}%",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            MinWidth = 40,
            TextAlignment = TextAlignment.Right,
        };

        slider.ValueChanged += (s, e) =>
        {
            var val = (int)e.NewValue;
            monitor.CurrentBrightness = val;
            pctText.Text = $"{val}%";
            _brightnessUpdates.SetLatest(monitor.PhysicalMonitorHandle, monitor.DisplayName, val);
        };

        return (slider, pctText);
    }

    private static UIElement CreateSliderGrid(Slider slider, TextBlock pctText)
    {
        var sliderRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(slider, 0);
        Grid.SetColumn(pctText, 1);
        sliderRow.Children.Add(slider);
        sliderRow.Children.Add(pctText);

        return sliderRow;
    }
}
