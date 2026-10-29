using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using AionMeter.Core;

namespace AionMeter;

public partial class DetailsWindow : Window
{
    readonly MeterController controller;
    readonly ObservableCollection<CombatantRow> rows = [];

    public DetailsWindow(MeterController controller)
    {
        this.controller = controller;
        InitializeComponent();

        PlayersGrid.ItemsSource = rows;
        var view = (ListCollectionView)CollectionViewSource.GetDefaultView(rows);
        view.IsLiveSorting = true;
        foreach (var p in new[] { "Rank", "Name", "ClassName", "Damage", "Dps", "Percent", "Hits", "CritRate", "MaxHit" })
            view.LiveSortingProperties.Add(p);

        MonstersBox.IsChecked = controller.Settings.ShowMonsters;

        controller.Updated += Refresh;
        Closed += (_, _) => controller.Updated -= Refresh;
        Refresh();
    }

    /// <summary>Selects a player by their log name, if they're in the list.</summary>
    public void SelectPlayer(string key)
    {
        var row = rows.FirstOrDefault(r => r.Key == key);
        if (row != null)
        {
            PlayersGrid.SelectedItem = row;
            PlayersGrid.ScrollIntoView(row);
        }
    }

    void Refresh()
    {
        var enc = controller.Tracker.Current;
        var ranked = enc.Ranked(controller.Settings.ShowMonsters);
        CombatantRow.Sync(rows, ranked, enc);

        var total = ranked.Sum(c => c.Damage);
        var secs = enc.Seconds;
        SummaryText.Text = enc.IsEmpty
            ? "No damage yet"
            : $"{Format.Duration(secs)}   Total {Format.Number(total)}   DPS {Format.Number(secs > 0 ? total / secs : 0)}   Taken {Format.Number(enc.DamageTaken)}";

        UpdateSkills();
    }

    void UpdateSkills()
    {
        if (PlayersGrid.SelectedItem is CombatantRow row
            && controller.Tracker.Current.Combatants.TryGetValue(row.Key, out var c))
        {
            SkillsTitle.Text = $"{c.DisplayName} - skills";
            SkillsGrid.ItemsSource = SkillRow.From(c);
        }
        else
        {
            SkillsTitle.Text = "Select a player to see their skills";
            SkillsGrid.ItemsSource = null;
        }
    }

    void Copy_Click(object sender, RoutedEventArgs e)
    {
        var copied = Report.Copy(controller.Tracker.Current, rows);
        CopyButton.Content = copied ? "Copied!" : "Nothing to copy";
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => { timer.Stop(); CopyButton.Content = "Copy all"; };
        timer.Start();
    }

    void MonstersBox_Click(object sender, RoutedEventArgs e)
    {
        controller.Settings.ShowMonsters = MonstersBox.IsChecked == true;
        controller.Settings.Save();
        controller.ApplySettings();
        Refresh();
    }

    void PlayersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSkills();

    void RowMenu_Opened(object sender, RoutedEventArgs e)
    {
        RowMenu.Items.Clear();
        if (PlayersGrid.SelectedItem is not CombatantRow row || row.IsMonster)
        {
            RowMenu.Items.Add(new MenuItem { Header = "Select a player first", IsEnabled = false });
            return;
        }

        if (row.IsYou)
        {
            RowMenu.Items.Add(new MenuItem { Header = "Set your own class in Settings", IsEnabled = false });
            return;
        }

        var auto = new MenuItem { Header = "Auto-detect class" };
        auto.Click += (_, _) => controller.SetClass(row.Name, AionClass.Unknown);
        RowMenu.Items.Add(auto);
        RowMenu.Items.Add(new Separator());
        foreach (var cls in AionClassInfo.Playable)
        {
            var item = new MenuItem
            {
                Header = cls.ToString(),
                Icon = new Image { Source = ClassIcons.Get(cls), Width = 16, Height = 16 },
                IsChecked = row.Class == cls,
            };
            item.Click += (_, _) => controller.SetClass(row.Name, cls);
            RowMenu.Items.Add(item);
        }
    }
}
