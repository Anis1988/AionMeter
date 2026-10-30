using System.Text.RegularExpressions;

namespace AionMeter.Core;

public sealed class TrackerOptions
{
    /// <summary>Your character name; shown instead of "You".</summary>
    public string YourName { get; set; } = "";
    public AionClass YourClass { get; set; } = AionClass.Unknown;

    /// <summary>Count your summons' damage (spirits, servants) as yours.</summary>
    public bool MergeSummons { get; set; } = true;

    /// <summary>A monster that takes this much damage in one life counts as a boss.</summary>
    public long BossDamageThreshold { get; set; } = 1_500_000;
}

/// <summary>Turns combat events into per-fight damage tables.</summary>
public sealed class CombatTracker
{
    public const string UnknownDotName = "Other (DoT)";
    public const string UnknownHotName = "Other (heal over time)";
    public const string UnknownSummonName = "Other (summons)";

    // Aion character names are a single word; mobs usually have spaces in theirs.
    static readonly Regex PlayerName = new(@"^[\p{L}\p{Nd}]+$", RegexOptions.Compiled);
    static readonly Regex OwnedPet = new(@"^(?<owner>[\p{L}\p{Nd}]+)'s (?<pet>.+)$", RegexOptions.Compiled);

    readonly Dictionary<(string Target, string Skill), string> dotOwners = new();
    readonly Dictionary<string, string> lastSkillUser = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<(string Target, string Skill), string> hotOwners = new();
    readonly Dictionary<string, string> lastHealer = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, long> damageThisLife = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> detectedBosses = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> summonOwners = new(StringComparer.OrdinalIgnoreCase);

    public CombatTracker(TrackerOptions? options = null, ClassDetector? detector = null,
        IEnumerable<KeyValuePair<string, AionClass>>? classOverrides = null)
    {
        Options = options ?? new TrackerOptions();
        Detector = detector ?? new ClassDetector();
        if (classOverrides != null)
            foreach (var (name, cls) in classOverrides)
                ClassOverrides[name] = cls;
    }

    public TrackerOptions Options { get; }
    public ClassDetector Detector { get; }

    /// <summary>Classes set by hand, by character name.</summary>
    public Dictionary<string, AionClass> ClassOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Monsters marked as bosses by hand (saved between runs).</summary>
    public HashSet<string> KnownBosses { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsBoss(string name) => KnownBosses.Contains(name) || detectedBosses.Contains(name);

    /// <summary>Everything since the last reset; it keeps adding up until <see cref="Reset"/>.</summary>
    public Encounter Current { get; private set; } = new();

    /// <summary>Changes whenever the data changes, so the UI can skip redundant redraws.</summary>
    public int Version { get; private set; }

    /// <summary>Set when a skill was learned, so it can be saved.</summary>
    public bool LearnedSomething { get; set; }

    public void Process(CombatEvent e)
    {
        if (e.Kind == CombatEventKind.Summon)
        {
            summonOwners[e.Target] = e.Source;
            return;
        }
        if (e.Kind == CombatEventKind.Kill)
        {
            // It died, so the next one with this name starts from zero.
            damageThisLife.Remove(e.Target);
            return;
        }
        if (e.Amount <= 0)
        {
            // A fully absorbed hit still tells us which class used the skill.
            if (e.Kind == CombatEventKind.Hit && Detector.Lookup(e.Skill) is var cls && cls != AionClass.Unknown
                && !IsMonsterName(e.Source))
            {
                var attacker = GetOrAdd(Current, e.Source);
                attacker.Vote(cls);
                attacker.Class = ResolveClass(attacker);
                Version++;
            }
            return;
        }
        if (e.Kind is CombatEventKind.Heal or CombatEventKind.HealOverTime)
        {
            ProcessHeal(e);
            return;
        }

        string? owner = e.Source;
        var skill = e.Skill;
        var learnable = true;

        if (e.Kind == CombatEventKind.DamageOverTime)
        {
            owner = FindOwner(dotOwners, lastSkillUser, e.Target, skill);
            learnable = false;
        }
        else if (skill != null)
        {
            dotOwners[(e.Target, skill)] = owner;
            lastSkillUser[skill] = owner;
        }

        if (IsYou(e.Target))
        {
            Current.Advance(e.Time);
            Current.DamageTaken += e.Amount;
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
        else if (Options.MergeSummons && ClassDetector.SummonClass(owner) != AionClass.Unknown
                 && !AttacksPlayer(e.Target))
        {
            skill = PetSkill(owner, skill);
            owner = SummonOwner(owner) ?? UnknownSummonName;
            learnable = false;
        }

        if (string.Equals(owner, e.Target, StringComparison.OrdinalIgnoreCase))
            return;

        Current.Advance(e.Time);
        var hint = learnable ? Detector.Lookup(skill) : AionClass.Unknown;
        var c = GetOrAdd(Current, owner);
        c.Add(skill ?? "Auto-attack", e.Amount, e.Critical);
        c.Vote(hint);
        c.Class = ResolveClass(c);
        if (!c.IsMonster)
        {
            Current.AddTargetDamage(e.Target, e.Amount);
            TrackBossDamage(e.Target, e.Amount);
        }

        if (learnable && !IsMonsterName(owner))
        {
            var known = KnownClass(owner);
            if (known != AionClass.Unknown && Detector.Learn(skill, known))
                LearnedSomething = true;
        }

        Version++;
    }

    void ProcessHeal(CombatEvent e)
    {
        string? healer = e.Source;
        var skill = e.Skill;
        var learnable = true;

        if (e.Kind == CombatEventKind.HealOverTime)
        {
            healer = FindOwner(hotOwners, lastHealer, e.Target, skill) ?? UnknownHotName;
            learnable = false;
        }
        else if (skill != null)
        {
            hotOwners[(e.Target, skill)] = healer;
            lastHealer[skill] = healer;
        }

        // Healing your summon (spirit armour etc.) counts as yours.
        if (Options.MergeSummons && ClassDetector.SummonClass(healer) != AionClass.Unknown)
        {
            var owner = SummonOwner(healer);
            if (owner == null)
                return;
            skill = PetSkill(healer, skill);
            healer = owner;
            learnable = false;
        }

        // Monsters healing themselves aren't interesting.
        if (IsMonsterName(healer))
            return;

        Current.Advance(e.Time);
        var c = GetOrAdd(Current, healer);
        c.AddHeal(skill ?? "Heal", e.Amount, e.Critical);
        if (learnable)
            c.Vote(Detector.Lookup(skill));
        c.Class = ResolveClass(c);

        if (learnable)
        {
            var known = KnownClass(healer);
            if (known != AionClass.Unknown && Detector.Learn(skill, known))
                LearnedSomething = true;
        }

        Version++;
    }

    void TrackBossDamage(string target, long amount)
    {
        if (IsYou(target) || Current.Combatants.TryGetValue(target, out var t) && !t.IsMonster && !t.IsBoss
            && t.Class != AionClass.Unknown)
            return;
        var total = damageThisLife.GetValueOrDefault(target) + amount;
        damageThisLife[target] = total;
        if (total >= Options.BossDamageThreshold && detectedBosses.Add(target))
            ApplyBoss(target);
    }

    /// <summary>Marks or unmarks a monster as a boss by hand.</summary>
    public void SetBoss(string name, bool isBoss)
    {
        if (isBoss)
            KnownBosses.Add(name);
        else
        {
            KnownBosses.Remove(name);
            detectedBosses.Remove(name);
        }
        ApplyBoss(name);
    }

    void ApplyBoss(string name)
    {
        if (Current.Combatants.TryGetValue(name, out var c))
        {
            c.IsBoss = IsBoss(name);
            c.IsMonster = c.IsBoss || IsMonsterName(name);
            c.Class = ResolveClass(c);
        }
        Version++;
    }

    /// <summary>Clears all damage and healing and starts over.</summary>
    public void Reset()
    {
        damageThisLife.Clear();
        detectedBosses.Clear();
        summonOwners.Clear();
        dotOwners.Clear();
        lastSkillUser.Clear();
        hotOwners.Clear();
        lastHealer.Clear();
        Current = new Encounter();
        Version++;
    }

    /// <summary>Sets a character's class by hand; Unknown goes back to auto-detect.</summary>
    public void SetClass(string displayName, AionClass cls)
    {
        if (cls == AionClass.Unknown)
            ClassOverrides.Remove(displayName);
        else
            ClassOverrides[displayName] = cls;

        foreach (var c in Current.Combatants.Values)
        {
            if (string.Equals(c.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
            {
                c.Class = ResolveClass(c);
                if (cls != AionClass.Unknown && !c.IsMonster)
                    foreach (var skill in c.Skills.Keys.Concat(c.HealSkills.Keys))
                        LearnedSomething |= Detector.Learn(skill, cls);
            }
        }
        Version++;
    }

    /// <summary>Re-applies options (name/class) after settings change.</summary>
    public void RefreshClasses()
    {
        foreach (var c in Current.Combatants.Values)
            c.Class = ResolveClass(c);
        Version++;
    }

    public static bool IsMonsterName(string name) =>
        name != LogParser.You && name != UnknownDotName && name != UnknownHotName && name != UnknownSummonName
        && !PlayerName.IsMatch(name);

    bool IsYou(string name) =>
        name == LogParser.You
        || (Options.YourName.Length > 0 && string.Equals(name, Options.YourName, StringComparison.OrdinalIgnoreCase));

    string DisplayNameOf(string name) =>
        name == LogParser.You && Options.YourName.Length > 0 ? Options.YourName : name;

    Combatant GetOrAdd(Encounter enc, string name)
    {
        if (IsYou(name))
            name = LogParser.You;
        if (!enc.Combatants.TryGetValue(name, out var c))
        {
            var boss = name != LogParser.You && IsBoss(name);
            c = new Combatant(name, DisplayNameOf(name), name == LogParser.You, boss || IsMonsterName(name))
            {
                IsBoss = boss,
            };
            enc.Combatants[name] = c;
        }
        return c;
    }

    static string? FindOwner(Dictionary<(string, string), string> byTarget,
        Dictionary<string, string> bySkill, string target, string? skill)
    {
        if (skill == null)
            return null;
        if (byTarget.TryGetValue((target, skill), out var owner))
            return owner;
        return bySkill.GetValueOrDefault(skill);
    }

    /// <summary>
    /// Who a summon belongs to: whoever was seen summoning it, else you if you're that class,
    /// else the only player of that class in the fight. Null if it can't be told.
    /// </summary>
    string? SummonOwner(string pet)
    {
        if (summonOwners.TryGetValue(pet, out var summoner))
            return summoner;

        var cls = ClassDetector.SummonClass(pet);
        var yours = ClassOf(LogParser.You);
        if (yours == cls)
            return LogParser.You;

        var players = Current.Combatants.Values
            .Where(c => !c.IsMonster && !c.IsYou && c.Class == cls)
            .Select(c => c.Name)
            .ToList();
        if (players.Count == 1)
            return players[0];

        // Your class isn't known yet and nobody else is that class: most likely yours.
        return yours == AionClass.Unknown && players.Count == 0 ? LogParser.You : null;
    }

    /// <summary>A "spirit" hitting you or another player is a monster, not someone's summon.</summary>
    bool AttacksPlayer(string target) =>
        IsYou(target) || Current.Combatants.TryGetValue(target, out var t) && !t.IsMonster;

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
