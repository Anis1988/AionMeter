using System.Text.RegularExpressions;

namespace AionMeter.Core;

public sealed class TrackerOptions
{
    /// <summary>Your character name; shown instead of "You".</summary>
    public string YourName { get; set; } = "";
    public AionClass YourClass { get; set; } = AionClass.Unknown;

    /// <summary>Seconds without damage before a new fight starts.</summary>
    public int IdleSeconds { get; set; } = 20;

    /// <summary>Count your summons' damage (spirits, servants) as yours.</summary>
    public bool MergeSummons { get; set; } = true;
}

/// <summary>Turns combat events into per-fight damage tables.</summary>
public sealed class CombatTracker
{
    public const string UnknownDotName = "Other (DoT)";
    const int MaxHistory = 30;

    // Aion character names are a single word; mobs usually have spaces in theirs.
    static readonly Regex PlayerName = new(@"^[\p{L}\p{Nd}]+$", RegexOptions.Compiled);
    static readonly Regex OwnedPet = new(@"^(?<owner>[\p{L}\p{Nd}]+)'s (?<pet>.+)$", RegexOptions.Compiled);

    readonly List<Encounter> history = [];
    readonly Dictionary<(string Target, string Skill), string> dotOwners = new();
    readonly Dictionary<string, string> lastSkillUser = new(StringComparer.OrdinalIgnoreCase);
    double archivedSeconds;
    int fightNumber;

    public CombatTracker(TrackerOptions? options = null, ClassDetector? detector = null,
        IEnumerable<KeyValuePair<string, AionClass>>? classOverrides = null)
    {
        Options = options ?? new TrackerOptions();
        Detector = detector ?? new ClassDetector();
        if (classOverrides != null)
            foreach (var (name, cls) in classOverrides)
                ClassOverrides[name] = cls;
        Current = new Encounter(++fightNumber);
    }

    public TrackerOptions Options { get; }
    public ClassDetector Detector { get; }

    /// <summary>Classes set by hand, by character name.</summary>
    public Dictionary<string, AionClass> ClassOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Encounter Current { get; private set; }
    public Encounter Session { get; private set; } = new(0) { FixedSeconds = 0 };

    /// <summary>Finished fights, newest first.</summary>
    public IReadOnlyList<Encounter> History => history;

    /// <summary>The fight to show: the running one, or the last finished one while idle.</summary>
    public Encounter Latest => Current.IsEmpty && history.Count > 0 ? history[0] : Current;

    /// <summary>Changes whenever the data changes, so the UI can skip redundant redraws.</summary>
    public int Version { get; private set; }

    /// <summary>Set when a skill was learned, so it can be saved.</summary>
    public bool LearnedSomething { get; set; }

    public void Process(CombatEvent e)
    {
        if (e.Amount <= 0)
            return;

        string? owner = e.Source;
        var skill = e.Skill;
        var learnable = true;

        if (e.Kind == CombatEventKind.DamageOverTime)
        {
            owner = FindDotOwner(e.Target, skill);
            learnable = false;
        }
        else if (skill != null)
        {
            dotOwners[(e.Target, skill)] = owner;
            lastSkillUser[skill] = owner;
        }

        if (IsYou(e.Target))
        {
            Advance(e.Time);
            Current.DamageTaken += e.Amount;
            Session.DamageTaken += e.Amount;
            Version++;
        }

        if (owner == null)
        {
            // An unowned tick on a player is most likely a monster's DoT; skip it.
            if (!IsMonsterName(e.Target))
                return;
            owner = UnknownDotName;
        }
        var petMatch = OwnedPet.Match(owner);
        if (petMatch.Success)
        {
            skill = PetSkill(petMatch.Groups["pet"].Value, skill);
            owner = petMatch.Groups["owner"].Value;
            learnable = false;
        }
        else if (Options.MergeSummons && ClassDetector.SummonClass(owner) is var summonClass
                 && summonClass != AionClass.Unknown && summonClass == ClassOf(LogParser.You))
        {
            skill = PetSkill(owner, skill);
            owner = LogParser.You;
            learnable = false;
        }

        if (string.Equals(owner, e.Target, StringComparison.OrdinalIgnoreCase))
            return;

        Advance(e.Time);
        var hint = learnable ? Detector.Lookup(skill) : AionClass.Unknown;
        foreach (var enc in new[] { Current, Session })
        {
            var c = GetOrAdd(enc, owner);
            c.Add(skill ?? "Auto-attack", e.Amount, e.Critical);
            c.Vote(hint);
            c.Class = ResolveClass(c);
            if (!c.IsMonster)
                enc.AddTargetDamage(e.Target, e.Amount);
        }

        if (learnable && !IsMonsterName(owner))
        {
            var known = KnownClass(owner);
            if (known != AionClass.Unknown && Detector.Learn(skill, known))
                LearnedSomething = true;
        }

        Session.FixedSeconds = archivedSeconds + Current.Seconds;
        Version++;
    }

    /// <summary>Clears all fights.</summary>
    public void Reset()
    {
        history.Clear();
        dotOwners.Clear();
        lastSkillUser.Clear();
        archivedSeconds = 0;
        fightNumber = 0;
        Current = new Encounter(++fightNumber);
        Session = new Encounter(0) { FixedSeconds = 0 };
        Version++;
    }

    /// <summary>Sets a character's class by hand; Unknown goes back to auto-detect.</summary>
    public void SetClass(string displayName, AionClass cls)
    {
        if (cls == AionClass.Unknown)
            ClassOverrides.Remove(displayName);
        else
            ClassOverrides[displayName] = cls;

        foreach (var enc in history.Append(Current).Append(Session))
            foreach (var c in enc.Combatants.Values)
            {
                if (string.Equals(c.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    c.Class = ResolveClass(c);
                    if (cls != AionClass.Unknown && !c.IsMonster)
                        foreach (var skill in c.Skills.Keys)
                            LearnedSomething |= Detector.Learn(skill, cls);
                }
            }
        Version++;
    }

    /// <summary>Re-applies options (name/class) to every fight after settings change.</summary>
    public void RefreshClasses()
    {
        foreach (var enc in history.Append(Current).Append(Session))
            foreach (var c in enc.Combatants.Values)
                c.Class = ResolveClass(c);
        Version++;
    }

    public static bool IsMonsterName(string name) =>
        name != LogParser.You && name != UnknownDotName && !PlayerName.IsMatch(name)
        && ClassDetector.SummonClass(name) == AionClass.Unknown;

    bool IsYou(string name) =>
        name == LogParser.You
        || (Options.YourName.Length > 0 && string.Equals(name, Options.YourName, StringComparison.OrdinalIgnoreCase));

    string DisplayNameOf(string name) =>
        name == LogParser.You && Options.YourName.Length > 0 ? Options.YourName : name;

    void Advance(DateTime time)
    {
        if (Current.End is { } end
            && ((time - end).TotalSeconds > Options.IdleSeconds || time < end.AddMinutes(-1)))
        {
            if (!Current.IsEmpty)
            {
                archivedSeconds += Current.Seconds;
                history.Insert(0, Current);
                if (history.Count > MaxHistory)
                    history.RemoveAt(history.Count - 1);
            }
            Current = new Encounter(++fightNumber);
            dotOwners.Clear();
        }

        Current.Start ??= time;
        if (Current.End == null || time > Current.End)
            Current.End = time;
        Session.Start ??= time;
        if (Session.End == null || time > Session.End)
            Session.End = time;
    }

    Combatant GetOrAdd(Encounter enc, string name)
    {
        if (IsYou(name))
            name = LogParser.You;
        if (!enc.Combatants.TryGetValue(name, out var c))
        {
            c = new Combatant(name, DisplayNameOf(name), name == LogParser.You, IsMonsterName(name));
            enc.Combatants[name] = c;
        }
        return c;
    }

    string? FindDotOwner(string target, string? skill)
    {
        if (skill == null)
            return null;
        if (dotOwners.TryGetValue((target, skill), out var owner))
            return owner;
        return lastSkillUser.GetValueOrDefault(skill);
    }

    static string PetSkill(string pet, string? skill) => skill == null ? pet : $"{pet}: {skill}";

    /// <summary>A class we're sure of: set by hand, or your own from settings.</summary>
    AionClass KnownClass(string name)
    {
        if (IsYou(name))
            return Options.YourClass;
        return ClassOverrides.GetValueOrDefault(DisplayNameOf(name));
    }

    AionClass ClassOf(string name)
    {
        var known = KnownClass(name);
        if (known != AionClass.Unknown)
            return known;
        return Current.Combatants.GetValueOrDefault(IsYou(name) ? LogParser.You : name)?.DetectedClass
               ?? AionClass.Unknown;
    }

    AionClass ResolveClass(Combatant c)
    {
        if (c.IsMonster)
            return AionClass.Unknown;
        var summon = ClassDetector.SummonClass(c.Name);
        if (summon != AionClass.Unknown)
            return summon;
        var known = KnownClass(c.Name);
        return known != AionClass.Unknown ? known : c.DetectedClass;
    }
}
