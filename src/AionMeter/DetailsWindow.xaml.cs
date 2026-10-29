using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AionMeter.Core;

namespace AionMeter;

public partial class DetailsWindow : Window
{
    /// <summary>An entry in the fight picker.</summary>
    sealed class FightChoice(string title, Func<Encounter> resolve, int key) : Observable
    {
        string title = title;

        public string Title { get => title; set => Set(ref title, value); }
        public Func<Encounter> Resolve { get; } = resolve;
        public int Key { get; } = key;
    }

    const int LiveKey = -1;
    const int SessionKey = 0;

    readonly MeterController controller;
    readonly ObservableCollection<CombatantRow> rows = [];
    readonly ObservableCollection<FightChoice> fights = [];
    Encounter? shown;
    bool updatingFights;

    public DetailsWindow(MeterController controller)
    {
        this.controller = controller;
        InitializeComponent();

        PlayersGrid.ItemsSource = rows;
        var view = (ListCollectionView)CollectionViewSource.GetDefaultView(rows);
        view.IsLiveSorting = true;
        foreach (var p in new[] { "Rank", "Name", "ClassName", "Damage", "Dps", "Percent", "Hits", "CritRate", "MaxHit" })
            view.LiveSortingProperties.Add(p);

        EncounterBox.ItemsSource = fights;
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
        UpdateFightList();
        var choice = EncounterBox.SelectedItem as FightChoice ?? fights[0];
        var enc = choice.Resolve();
        shown = enc;

        var ranked = enc.Ranked(controller.Settings.ShowMonsters);
        CombatantRow.Sync(rows, ranked, enc);

        var total = ranked.Sum(c => c.Damage);
        var secs = enc.Seconds;
        SummaryText.Text = enc.IsEmpty
            ? "No damage yet"
            : $"{Format.Duration(secs)}   Total {Format.Number(total)}   DPS {Format.Number(secs > 0 ? total / secs : 0)}   Taken {Format.Number(enc.DamageTaken)}";

        UpdateSkills();
    }

    void UpdateFightList()
    {
        updatingFights = true;
        var selectedKey = (EncounterBox.SelectedItem as FightChoice)?.Key ?? LiveKey;
        var tracker = controller.Tracker;

        var wanted = new List<FightChoice>
        {
            new("Current fight (live) - " + tracker.Latest.Title, () => controller.Tracker.Latest, LiveKey),
            new("Whole session", () => controller.Tracker.Session, SessionKey),
        };
        foreach (var enc in tracker.History)
        {
            var e = enc;
            wanted.Add(new FightChoice($"{e.Title}  ({Format.Duration(e.Seconds)})", () => e, e.Number));
        }

        // Rebuild only when the list changed, so the open dropdown doesn't flicker.
        if (fights.Count != wanted.Count || fights.Zip(wanted).Any(p => p.First.Key != p.Second.Key))
        {
            fights.Clear();
            foreach (var f in wanted)
                fights.Add(f);
        }
        else
        {
            fights[0].Title = wanted[0].Title;
        }

        EncounterBox.SelectedItem = fights.FirstOrDefault(f => f.Key == selectedKey) ?? fights[0];
        updatingFights = false;
    }

    void UpdateSkills()
    {
        if (PlayersGrid.SelectedItem is CombatantRow row && shown != null
            && shown.Combatants.TryGetValue(row.Key, out var c))
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

    void EncounterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!updatingFights && IsLoaded)
            Refresh();
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
