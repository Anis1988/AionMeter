using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AionMeter.Core;
using Microsoft.Win32;

namespace AionMeter;

public partial class SettingsWindow : Window
{
    readonly Settings settings;

    readonly MeterController controller;

    public SettingsWindow(MeterController controller)
    {
        this.controller = controller;
        var settings = this.settings = controller.Settings;
        InitializeComponent();

        GamePathBox.Text = settings.GamePath;
        NameBox.Text = settings.CharacterName;
        ClassBox.ItemsSource = Enum.GetValues<AionClass>().Select(c => c == AionClass.Unknown ? "Auto-detect" : c.ToString());
        ClassBox.SelectedIndex = (int)settings.YourClass;
        CopyCountBox.Text = settings.CopyCount.ToString();
        BossBox.Text = settings.BossDamageThreshold.ToString("N0");
        OpacitySlider.Value = settings.Opacity;
        MonstersBox.IsChecked = settings.ShowMonsters;
        SummonsBox.IsChecked = settings.MergeSummons;
        ClearLogBox.IsChecked = settings.ClearChatLogOnStart;
        ShowLogSize();
        UnreadText.Text = controller.UnreadCount == 0 ? "None so far" : $"{controller.UnreadCount} this session";

        GamePathBox.TextChanged += (_, _) => CheckPath();
        CheckPath();
    }

    void CheckPath()
    {
        var folder = GamePathBox.Text.Trim();
        var ok = File.Exists(Path.Combine(folder, "Chat.log"));
        var game = Directory.Exists(Path.Combine(folder, "bin64")) || Directory.Exists(Path.Combine(folder, "bin32"));
        PathStatus.Text = ok ? "Chat.log found"
            : game ? "Game folder found, but no Chat.log yet - enable g_chatlog in system.cfg and log in once"
            : "This doesn't look like the Aion folder (no bin32/bin64)";
        PathStatus.Foreground = ok ? Brushes.LightGreen : Brushes.Orange;
    }

    void ShowLogSize()
    {
        var path = Path.Combine(GamePathBox.Text.Trim(), "Chat.log");
        LogSizeText.Text = File.Exists(path)
            ? $"Current size: {new FileInfo(path).Length / 1024.0 / 1024.0:0.0} MB"
            : "";
    }

    void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        var message = controller.ClearChatLog();
        ShowLogSize();
        LogSizeText.Text = message;
    }

    void OpenUnread_Click(object sender, RoutedEventArgs e)
    {
        var file = MeterController.UnreadFile;
        if (!File.Exists(file))
        {
            UnreadText.Text = "Nothing unread yet";
            return;
        }
        // Show the file selected in Explorer so it's easy to attach or upload.
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file}\"");
    }

    void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select your Aion game folder" };
        if (Directory.Exists(GamePathBox.Text))
            dialog.InitialDirectory = GamePathBox.Text;
        if (dialog.ShowDialog(this) == true)
            GamePathBox.Text = dialog.FolderName;
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(CopyCountBox.Text.Trim(), out var copyCount) || copyCount < 0 || copyCount > 100)
        {
            MessageBox.Show(this, "Players to copy must be a number from 0 to 100 (0 = everyone).", "Aion Meter");
            return;
        }

        var bossText = new string(BossBox.Text.Where(char.IsAsciiDigit).ToArray());
        if (!long.TryParse(bossText, out var bossThreshold) || bossThreshold < 10_000)
        {
            MessageBox.Show(this, "Boss detection must be a number of at least 10,000.", "Aion Meter");
            return;
        }

        settings.BossDamageThreshold = bossThreshold;
        settings.GamePath = GamePathBox.Text.Trim();
        settings.CharacterName = NameBox.Text.Trim();
        settings.YourClass = (AionClass)Math.Max(0, ClassBox.SelectedIndex);
        settings.CopyCount = copyCount;
        settings.Opacity = OpacitySlider.Value;
        settings.ShowMonsters = MonstersBox.IsChecked == true;
        settings.MergeSummons = SummonsBox.IsChecked == true;
        settings.ClearChatLogOnStart = ClearLogBox.IsChecked == true;
        DialogResult = true;
    }
}
