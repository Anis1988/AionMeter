using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AionMeter.Core;

namespace AionMeter;

/// <summary>Toggle buttons that show/hide each class.</summary>
public sealed class FilterPanel : WrapPanel
{
    readonly MeterController controller;
    readonly Dictionary<AionClass, ToggleButton> classToggles = [];

    public FilterPanel(MeterController controller)
    {
        this.controller = controller;
        var settings = controller.Settings;

        foreach (var cls in Enum.GetValues<AionClass>())
        {
            var label = cls == AionClass.Unknown ? "Unknown class" : cls.ToString();
            var toggle = Toggle(ClassIcons.Get(cls), label, !settings.HiddenClasses.Contains(cls), on =>
            {
                if (on) settings.HiddenClasses.Remove(cls);
                else settings.HiddenClasses.Add(cls);
            });
            classToggles[cls] = toggle;
        }

        var all = new Button
        {
            Content = "All",
            Style = (Style)Application.Current.FindResource("DarkButton"),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(6, 2, 2, 2),
            ToolTip = "Show every class",
        };
        all.Click += (_, _) =>
        {
            settings.HiddenClasses.Clear();
            Sync();
            controller.ApplyFilters();
        };
        Children.Add(all);
    }

    /// <summary>Re-reads the toggles' state from settings.</summary>
    public void Sync()
    {
        var s = controller.Settings;
        foreach (var (cls, toggle) in classToggles)
            toggle.IsChecked = !s.HiddenClasses.Contains(cls);
    }

    ToggleButton Toggle(System.Windows.Media.ImageSource icon, string label, bool on, Action<bool> apply)
    {
        var toggle = new ToggleButton
        {
            Style = (Style)Application.Current.FindResource("FilterToggle"),
            Content = new Image { Source = icon },
            IsChecked = on,
            ToolTip = label,
        };
        toggle.Click += (_, _) =>
        {
            apply(toggle.IsChecked == true);
            controller.ApplyFilters();
        };
        Children.Add(toggle);
        return toggle;
    }
}
