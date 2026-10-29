using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AionMeter.Core;
using Microsoft.Win32;

namespace AionMeter;

public partial class SettingsWindow : Window
{
    readonly Settings settings;

    public SettingsWindow(Settings settings)
    {
        this.settings = settings;
        InitializeComponent();

        GamePathBox.Text = settings.GamePath;
        NameBox.Text = settings.CharacterName;
        ClassBox.ItemsSource = Enum.GetValues<AionClass>().Select(c => c == AionClass.Unknown ? "Auto-detect" : c.ToString());
        ClassBox.SelectedIndex = (int)settings.YourClass;
        IdleBox.Text = settings.IdleSeconds.ToString();
        OpacitySlider.Value = settings.Opacity;
        MonstersBox.IsChecked = settings.ShowMonsters;
        SummonsBox.IsChecked = settings.MergeSummons;

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
        if (!int.TryParse(IdleBox.Text, out var idle) || idle < 3 || idle > 600)
        {
            MessageBox.Show(this, "Enter a number of seconds between 3 and 600.", "Aion Meter");
            return;
        }

        settings.GamePath = GamePathBox.Text.Trim();
        settings.CharacterName = NameBox.Text.Trim();
        settings.YourClass = (AionClass)Math.Max(0, ClassBox.SelectedIndex);
        settings.IdleSeconds = idle;
        settings.Opacity = OpacitySlider.Value;
        settings.ShowMonsters = MonstersBox.IsChecked == true;
        settings.MergeSummons = SummonsBox.IsChecked == true;
        DialogResult = true;
    }
}
