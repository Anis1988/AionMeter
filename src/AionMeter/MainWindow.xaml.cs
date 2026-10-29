using System.Windows;
using System.Windows.Input;

namespace AionMeter;

public partial class MainWindow : Window
{
    readonly MeterController controller;
    DetailsWindow? details;

    public MainWindow(MeterController controller)
    {
        this.controller = controller;
        InitializeComponent();
        Bars.ItemsSource = controller.OverlayRows;
        controller.Updated += UpdateTexts;
        RestorePlacement();
        UpdateTexts();
    }

    void RestorePlacement()
    {
        var s = controller.Settings;
        Opacity = Math.Clamp(s.Opacity, 0.3, 1);
        Width = Math.Max(MinWidth, s.Width);
        Height = Math.Max(MinHeight, s.Height);
        if (double.IsNaN(s.Left) || double.IsNaN(s.Top)
            || s.Left < SystemParameters.VirtualScreenLeft - 50
            || s.Top < SystemParameters.VirtualScreenTop - 50
            || s.Left > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 50
            || s.Top > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 50)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else
        {
            Left = s.Left;
            Top = s.Top;
        }
    }

    void UpdateTexts()
    {
        var enc = controller.Tracker.Latest;
        var rows = controller.OverlayRows;
        var total = rows.Sum(r => r.Damage);
        var secs = enc.Seconds;

        HeaderInfo.Text = enc.IsEmpty ? "" : $"#{enc.Number}  {Format.Duration(secs)}";
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Footer.Text = controller.Status.Length > 0
            ? controller.Status
            : $"Total {Format.Number(total)}   DPS {Format.Number(secs > 0 ? total / secs : 0)}   Taken {Format.Number(enc.DamageTaken)}";
    }

    void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    void Bars_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ShowDetails();
        if ((e.OriginalSource as FrameworkElement)?.DataContext is CombatantRow row)
            details?.SelectPlayer(row.Key);
    }

    void Details_Click(object sender, RoutedEventArgs e) => ShowDetails();

    void ShowDetails()
    {
        if (details == null)
        {
            details = new DetailsWindow(controller) { Owner = this };
            details.Closed += (_, _) => details = null;
            details.Show();
        }
        else
        {
            if (details.WindowState == WindowState.Minimized)
                details.WindowState = WindowState.Normal;
            details.Activate();
        }
    }

    void Pause_Click(object sender, RoutedEventArgs e)
    {
        controller.Paused = !controller.Paused;
        PauseButton.Content = controller.Paused ? "" : "";
        PauseButton.ToolTip = controller.Paused ? "Resume" : "Pause";
    }

    void Reset_Click(object sender, RoutedEventArgs e) => controller.Reset();

    void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(controller.Settings) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            Opacity = Math.Clamp(controller.Settings.Opacity, 0.3, 1);
            controller.ApplySettings();
            controller.Settings.Save();
        }
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        var s = controller.Settings;
        if (WindowState == WindowState.Normal)
        {
            s.Left = Left;
            s.Top = Top;
            s.Width = Width;
            s.Height = Height;
        }
        controller.SaveLearned();
        base.OnClosing(e);
    }
}
