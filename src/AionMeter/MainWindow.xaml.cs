using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AionMeter.Core;

namespace AionMeter;

public partial class MainWindow : Window
{
    const double CubeSize = 68;     // cube plus its glow margin
    const double CollapsedHeight = 44;

    readonly MeterController controller;
    readonly FilterPanel filters;
    DetailsWindow? details;
    double expandedWidth, expandedHeight;

    public MainWindow(MeterController controller)
    {
        this.controller = controller;
        InitializeComponent();
        LogoImage.Source = CubeLogo.Source = ClassIcons.Logo;
        Bars.ItemsSource = controller.OverlayRows;
        filters = new FilterPanel(controller);
        FilterHost.Child = filters;
        controller.Updated += UpdateTexts;
        RestorePlacement();
        UpdateTexts();
        if (controller.Settings.CubeMode)
            Loaded += (_, _) => EnterCube();
    }

    bool Collapsed => Body.Visibility != Visibility.Visible;
    bool InCube => Cube.Visibility == Visibility.Visible;

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
        var enc = controller.Tracker.Current;
        var rows = controller.OverlayRows;
        var total = rows.Sum(r => r.Value);
        var secs = enc.Seconds;
        var perSecond = secs > 0 ? total / secs : 0;
        var healing = controller.Mode == MeterMode.Healing;

        ModeButton.Content = healing ? "HEAL" : "DMG";
        ModeButton.Foreground = healing ? Brushes.SpringGreen : Brushes.Orange;
        ModeButton.ToolTip = healing ? "Showing healing - click to show damage" : "Showing damage - click to show healing";
        EmptyText.Text = healing ? "No healing yet..." : "Waiting for combat...";
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HeaderInfo.Text = enc.IsEmpty ? "" : Format.Duration(secs);
        CubeText.Text = enc.IsEmpty ? "" : Format.Number(perSecond);

        Footer.Text = controller.Status.Length > 0
            ? controller.Status
            : healing
                ? $"Healed {Format.Number(total)}  ·  HPS {Format.Number(perSecond)}"
                : $"Total {Format.Number(total)}  ·  DPS {Format.Number(perSecond)}  ·  Taken {Format.Number(enc.DamageTaken)}";
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

    void Filter_Click(object sender, RoutedEventArgs e)
    {
        filters.Sync();
        FilterPopup.IsOpen = !FilterPopup.IsOpen;
    }

    void Pause_Click(object sender, RoutedEventArgs e)
    {
        controller.Paused = !controller.Paused;
        PauseButton.Content = controller.Paused ? "" : "";
        PauseButton.ToolTip = controller.Paused ? "Resume" : "Pause";
    }

    void Reset_Click(object sender, RoutedEventArgs e) => controller.Reset();

    void Mode_Click(object sender, RoutedEventArgs e) =>
        controller.Mode = controller.Mode == MeterMode.Damage ? MeterMode.Healing : MeterMode.Damage;

    void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (Report.Copy(controller.Tracker.Current, controller.OverlayRows, controller.Settings.CopyCount))
            Flash("Copied to clipboard");
    }

    void Flash(string message)
    {
        Footer.Text = message;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => { timer.Stop(); UpdateTexts(); };
        timer.Start();
    }

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

    /// <summary>Shrinks the overlay to just its title bar, or restores it.</summary>
    void Collapse_Click(object sender, RoutedEventArgs e)
    {
        if (Collapsed)
        {
            Body.Visibility = FooterBar.Visibility = Visibility.Visible;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Height = expandedHeight;
            CollapseButton.Content = "";
            CollapseButton.ToolTip = "Collapse to title bar";
        }
        else
        {
            expandedHeight = Height;
            Body.Visibility = FooterBar.Visibility = Visibility.Collapsed;
            ResizeMode = ResizeMode.NoResize;
            Height = CollapsedHeight;
            CollapseButton.Content = "";
            CollapseButton.ToolTip = "Expand";
        }
    }

    void Cube_Click(object sender, RoutedEventArgs e) => EnterCube();

    /// <summary>Hides everything behind a small cube; clicking it brings the meter back.</summary>
    void EnterCube()
    {
        if (InCube)
            return;
        FilterPopup.IsOpen = false;
        if (!Collapsed)
            expandedHeight = Height;
        expandedWidth = Width;
        Panel.Visibility = Visibility.Collapsed;
        Cube.Visibility = Visibility.Visible;
        ResizeMode = ResizeMode.NoResize;
        MinWidth = MinHeight = CubeSize;
        Width = Height = CubeSize;
        controller.Settings.CubeMode = true;
    }

    void ExitCube()
    {
        Cube.Visibility = Visibility.Collapsed;
        Panel.Visibility = Visibility.Visible;
        MinWidth = 320;
        MinHeight = CollapsedHeight;
        Width = Math.Max(MinWidth, expandedWidth);
        Height = Collapsed ? CollapsedHeight : Math.Max(120, expandedHeight);
        ResizeMode = Collapsed ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        controller.Settings.CubeMode = false;
    }

    void Cube_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var (left, top) = (Left, Top);
        DragMove();
        // A click without moving restores the meter; a drag just moves the cube.
        if (Math.Abs(Left - left) < 3 && Math.Abs(Top - top) < 3)
            ExitCube();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        var s = controller.Settings;
        if (WindowState == WindowState.Normal)
        {
            s.Left = Left;
            s.Top = Top;
            s.Width = InCube ? expandedWidth : Width;
            s.Height = InCube || Collapsed ? expandedHeight : Height;
        }
        controller.SaveLearned();
        base.OnClosing(e);
    }
}
