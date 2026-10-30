using System.Collections.ObjectModel;
using System.Windows.Threading;
using AionMeter.Core;

namespace AionMeter;

/// <summary>Reads Chat.log on a timer and keeps the tracker and overlay rows up to date.</summary>
public sealed class MeterController
{
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    ChatLogReader? reader;
    int shownVersion = -1;
    Encounter? shownEncounter;
    DateTime lastSave = DateTime.UtcNow;

    public MeterController(Settings settings)
    {
        Settings = settings;
        Tracker = new CombatTracker(
            new TrackerOptions(),
            new ClassDetector(settings.LearnedSkills),
            settings.ClassOverrides);
        Tracker.KnownBosses.UnionWith(settings.KnownBosses);
        ApplySettings();
        timer.Tick += (_, _) => Tick();
    }

    public Settings Settings { get; }
    public CombatTracker Tracker { get; }
    public ObservableCollection<CombatantRow> OverlayRows { get; } = [];
    public bool Paused { get; set; }

    /// <summary>Whether the meter ranks by damage or healing.</summary>
    public MeterMode Mode
    {
        get => Settings.Mode;
        set
        {
            if (Settings.Mode == value) return;
            Settings.Mode = value;
            Settings.Save();
            Refresh(force: true);
        }
    }
    public string Status { get; private set; } = "";

    /// <summary>Raised after new data was processed (or status changed).</summary>
    public event Action? Updated;

    public void Start()
    {
        if (Settings.ClearChatLogOnStart)
            ClearChatLog();
        Tick();
        timer.Start();
    }

    /// <summary>Applies changed settings; reopens the log if the folder changed.</summary>
    public void ApplySettings()
    {
        var o = Tracker.Options;
        o.YourName = Settings.CharacterName.Trim();
        o.YourClass = Settings.YourClass;
        o.MergeSummons = Settings.MergeSummons;
        o.BossDamageThreshold = Math.Max(10_000, Settings.BossDamageThreshold);
        if (reader == null || !string.Equals(reader.Path, Settings.ChatLogPath, StringComparison.OrdinalIgnoreCase))
            reader = new ChatLogReader(Settings.ChatLogPath);
        Tracker.RefreshClasses();
        shownVersion = -1;
    }

    public void Reset()
    {
        Tracker.Reset();
        Refresh(force: true);
    }

    public void SetClass(string name, AionClass cls)
    {
        Tracker.SetClass(name, cls);
        SaveLearned();
        Refresh(force: true);
    }

    /// <summary>Empties Chat.log; returns a message for the user.</summary>
    public string ClearChatLog()
    {
        try
        {
            var size = ChatLogReader.Clear(Settings.ChatLogPath);
            return size > 0 ? $"Cleared Chat.log ({size / 1024.0 / 1024.0:0.0} MB)" : "Chat.log is already empty";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "Couldn't clear Chat.log: " + ex.Message;
        }
    }

    /// <summary>Re-applies the class/monster filters.</summary>
    public void ApplyFilters()
    {
        Settings.Save();
        Refresh(force: true);
    }

    public void SetBoss(string name, bool isBoss)
    {
        Tracker.SetBoss(name, isBoss);
        Settings.KnownBosses = new(Tracker.KnownBosses, StringComparer.OrdinalIgnoreCase);
        Settings.Save();
        Refresh(force: true);
    }

    /// <summary>Whether a combatant passes the class/monster/boss filters.</summary>
    public bool IsVisible(Combatant c) =>
        c.IsBoss ? Settings.ShowBosses
        : c.IsMonster ? Settings.ShowMonsters
        : !Settings.HiddenClasses.Contains(c.Class);

    /// <summary>Filtered ranking for the current mode.</summary>
    public IReadOnlyList<Combatant> Visible(Encounter enc) =>
        enc.Ranked(includeMonsters: true, Mode).Where(IsVisible).ToList();

    public void SaveLearned()
    {
        Settings.ClassOverrides = new(Tracker.ClassOverrides, StringComparer.OrdinalIgnoreCase);
        Settings.LearnedSkills = new(Tracker.Detector.Learned, StringComparer.OrdinalIgnoreCase);
        Settings.Save();
        Tracker.LearnedSomething = false;
    }

    void Tick()
    {
        string status;
        try
        {
            var lines = reader!.ReadNewLines();
            if (!Paused)
                foreach (var line in lines)
                    if (LogParser.Parse(line) is { } e)
                        Tracker.Process(e);
            status = !reader.Exists
                ? "Chat.log not found - check the game folder in settings"
                : Paused ? "Paused" : "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status = "Can't read Chat.log: " + ex.Message;
        }

        var statusChanged = status != Status;
        Status = status;

        if (Tracker.LearnedSomething && DateTime.UtcNow - lastSave > TimeSpan.FromSeconds(30))
        {
            SaveLearned();
            lastSave = DateTime.UtcNow;
        }

        Refresh(force: statusChanged);
    }

    void Refresh(bool force)
    {
        var enc = Tracker.Current;
        if (!force && Tracker.Version == shownVersion && ReferenceEquals(enc, shownEncounter))
            return;
        shownVersion = Tracker.Version;
        shownEncounter = enc;
        CombatantRow.Sync(OverlayRows, Visible(enc), enc, Mode);
        Updated?.Invoke();
    }
}
