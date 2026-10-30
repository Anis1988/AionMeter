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
    readonly FilterPanel filters;

    public DetailsWindow(MeterController controller)
    {
        this.controller = controller;
        InitializeComponent();

        PlayersGrid.ItemsSource = rows;
        var view = (ListCollectionView)CollectionViewSource.GetDefaultView(rows);
        view.IsLiveSorting = true;
        foreach (var p in new[] { "Rank", "Name", "ClassName", "Damage", "Dps", "Percent", "Hits", "CritRate", "MaxHit",
                     "Healing", "Hps" })
            view.LiveSortingProperties.Add(p);

        FilterHost.Child = filters = new FilterPanel(controller);
        CopyButton.Content = CopyLabel;

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
        var mode = controller.Mode;
        ModeBox.SelectedIndex = (int)mode;
        var enc = controller.Tracker.Current;
        var ranked = controller.Visible(enc);
        filters.Sync();
        CombatantRow.Sync(rows, ranked, enc, mode);

        var total = ranked.Sum(c => c.Total(mode));
        var secs = enc.Seconds;
        var perSecond = Format.Number(secs > 0 ? total / secs : 0);
        SummaryText.Text = ranked.Count == 0
            ? mode == MeterMode.Healing ? "No healing yet" : "No damage yet"
            : mode == MeterMode.Healing
                ? $"{Format.Duration(secs)}   Healed {Format.Number(total)}   HPS {perSecond}"
                : $"{Format.Duration(secs)}   Total {Format.Number(total)}   DPS {perSecond}   Taken {Format.Number(enc.DamageTaken)}";

        UpdateSkills();
    }

    void UpdateSkills()
    {
        if (PlayersGrid.SelectedItem is CombatantRow row
            && controller.Tracker.Current.Combatants.TryGetValue(row.Key, out var c))
        {
            var mode = controller.Mode;
            SkillsTitle.Text = $"{c.DisplayName} - {(mode == MeterMode.Healing ? "heals" : "skills")}";
            SkillsGrid.ItemsSource = SkillRow.From(c, mode);
        }
        else
        {
            SkillsTitle.Text = "Select a player to see their skills";
            SkillsGrid.ItemsSource = null;
        }
    }

    void Copy_Click(object sender, RoutedEventArgs e)
    {
        var copied = Report.Copy(controller.Tracker.Current, rows, controller.Settings.CopyCount);
        CopyButton.Content = copied ? "Copied!" : "Nothing to copy";
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => { timer.Stop(); CopyButton.Content = CopyLabel; };
        timer.Start();
    }

    string CopyLabel => controller.Settings.CopyCount > 0 ? $"Copy top {controller.Settings.CopyCount}" : "Copy all";

    void ModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && ModeBox.SelectedIndex >= 0)
            controller.Mode = (MeterMode)ModeBox.SelectedIndex;
    }

    void PlayersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSkills();

    void RowMenu_Opened(object sender, RoutedEventArgs e)
    {
        RowMenu.Items.Clear();
        if (PlayersGrid.SelectedItem is not CombatantRow row)
        {
            RowMenu.Items.Add(new MenuItem { Header = "Select a player first", IsEnabled = false });
            return;
        }

        if (row.IsMonster || row.Class == AionClass.Unknown)
        {
            var boss = new MenuItem
            {
                Header = row.IsBoss ? "Not a boss" : "Mark as boss",
                Icon = new Image { Source = ClassIcons.Monster(true), Width = 16, Height = 16 },
            };
            boss.Click += (_, _) => controller.SetBoss(row.Key, !row.IsBoss);
            RowMenu.Items.Add(boss);
            if (row.IsMonster)
                return;
            RowMenu.Items.Add(new Separator());
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
