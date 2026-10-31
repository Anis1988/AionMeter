using System.Collections.ObjectModel;
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
    readonly ObservableCollection<PickOption> targetOptions = [];
    readonly ObservableCollection<PickOption> classOptions = [];
    DetailsWindow? details;
    double expandedWidth, expandedHeight;
    bool updatingPicks;

    public MainWindow(MeterController controller)
    {
        this.controller = controller;
        InitializeComponent();
        LogoImage.Source = CubeLogo.Source = ClassIcons.Logo;
        Bars.ItemsSource = controller.OverlayRows;
        filters = new FilterPanel(controller);
        FilterHost.Child = filters;
        TargetBox.ItemsSource = targetOptions;
        ClassPickBox.ItemsSource = classOptions;
        BuildClassOptions();
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
        var secs = controller.ViewSeconds(enc);
        var perSecond = secs > 0 ? total / secs : 0;
        var healing = controller.Mode == MeterMode.Healing;

        ModeButton.Content = healing ? "HEAL" : "DMG";
        ModeButton.Foreground = healing ? Brushes.SpringGreen : Brushes.Orange;
        ModeButton.ToolTip = healing ? "Showing healing - click to show damage" : "Showing damage - click to show healing";
        EmptyText.Text = healing ? "No healing yet..." : "Waiting for combat...";
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TargetBox.IsEnabled = !healing;
        UpdateTargetOptions();
        HeaderInfo.Text = enc.IsEmpty ? "" : Format.Duration(secs);
        CubeText.Text = enc.IsEmpty ? "" : Format.Number(perSecond);

        Footer.Text = controller.Status.Length > 0
            ? controller.Status
            : healing
                ? $"Healed {Format.Number(total)}  ·  HPS {Format.Number(perSecond)}"
                : $"Total {Format.Number(total)}  ·  DPS {Format.Number(perSecond)}  ·  Taken {Format.Number(enc.DamageTaken)}";
    }

    void BuildClassOptions()
    {
        updatingPicks = true;
        classOptions.Add(new PickOption("All classes", ClassIcons.Logo));
        foreach (var cls in AionClassInfo.Playable)
            classOptions.Add(new PickOption(cls.ToString(), ClassIcons.Get(cls)) { Class = cls });
        ClassPickBox.SelectedIndex = 0;
        updatingPicks = false;
    }

    /// <summary>Keeps the target list in step with the fight: everything players hit, most damaged first.</summary>
    void UpdateTargetOptions()
    {
        if (TargetBox.IsDropDownOpen)
            return;
        var enc = controller.Tracker.Current;
        var targets = enc.Targets.Take(40).ToList();
        var known = targetOptions.Skip(1).Select(o => o.Key!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        updatingPicks = true;
        if (targetOptions.Count == 0 || known.Count != targets.Count || targets.Any(t => !known.Contains(t.Name)))
        {
            targetOptions.Clear();
            targetOptions.Add(new PickOption("All targets", ClassIcons.Logo));
            foreach (var t in targets)
                targetOptions.Add(new PickOption("", null) { Key = t.Name });
        }
        foreach (var option in targetOptions.Skip(1))
            DescribeTarget(option, enc);

        var selected = targetOptions.FirstOrDefault(o =>
            o.Key != null && string.Equals(o.Key, controller.TargetFilter, StringComparison.OrdinalIgnoreCase));
        TargetBox.SelectedItem = selected ?? targetOptions[0];
        if (selected == null && controller.TargetFilter != null)
            controller.SetViewFilters(null, controller.ClassFilter);
        if (controller.ClassFilter == null && ClassPickBox.SelectedIndex != 0)
            ClassPickBox.SelectedIndex = 0;
        updatingPicks = false;
    }

    void DescribeTarget(PickOption option, Encounter enc)
    {
        var name = option.Key!;
        var boss = controller.Tracker.IsBoss(name);
        var shown = name == LogParser.You && controller.Settings.CharacterName.Length > 0
            ? controller.Settings.CharacterName
            : name;
        var damage = enc.Target(name)?.Damage ?? 0;
        option.Label = $"{(boss ? "BOSS " : "")}{shown}  ·  {Format.Number(damage)}";
        option.LabelBrush = boss ? ClassIcons.BossBrush : Brushes.White;
        option.Icon = boss ? ClassIcons.Monster(true)
            : CombatTracker.IsMonsterName(name) ? ClassIcons.Monster(false)
            : ClassIcons.Get(enc.Combatants.GetValueOrDefault(name)?.Class ?? AionClass.Unknown);
    }

    void Pick_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (updatingPicks)
            return;
        controller.SetViewFilters(
            (TargetBox.SelectedItem as PickOption)?.Key,
            (ClassPickBox.SelectedItem as PickOption)?.Class);
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
        var target = controller.TargetFilter == LogParser.You && controller.Settings.CharacterName.Length > 0
            ? controller.Settings.CharacterName
            : controller.TargetFilter;
        if (Report.Copy(controller.Tracker.Current, controller.OverlayRows, controller.Settings.CopyCount, target))
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
        var dialog = new SettingsWindow(controller) { Owner = this };
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
            Body.Visibility = FooterBar.Visibility = PickBar.Visibility = Visibility.Visible;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Height = expandedHeight;
            CollapseButton.Content = "";
            CollapseButton.ToolTip = "Collapse to title bar";
        }
        else
        {
            expandedHeight = Height;
            Body.Visibility = FooterBar.Visibility = PickBar.Visibility = Visibility.Collapsed;
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
