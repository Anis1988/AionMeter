namespace AionMeter.Core;

public sealed class SkillStats(string name)
{
    public string Name { get; } = name;
    public long Damage { get; private set; }
    public int Hits { get; private set; }
    public int Crits { get; private set; }
    public long MaxHit { get; private set; }

    public void Add(long amount, bool critical)
    {
        Damage += amount;
        Hits++;
        if (critical) Crits++;
        if (amount > MaxHit) MaxHit = amount;
    }
}

public sealed class Combatant(string name, string displayName, bool isYou, bool isMonster)
{
    readonly Dictionary<AionClass, int> votes = [];

    /// <summary>Name as it appears in the log ("You" for the log owner).</summary>
    public string Name { get; } = name;
    public string DisplayName { get; } = displayName;
    public bool IsYou { get; } = isYou;
    public bool IsMonster { get; internal set; } = isMonster;

    /// <summary>A monster that took boss-level damage, or one you marked as a boss.</summary>
    public bool IsBoss { get; internal set; }
    public AionClass Class { get; internal set; }

    public long Damage { get; private set; }
    public int Hits { get; private set; }
    public int Crits { get; private set; }
    public long MaxHit { get; private set; }
    public Dictionary<string, SkillStats> Skills { get; } = new(StringComparer.OrdinalIgnoreCase);

    public long Healing { get; private set; }
    public int HealHits { get; private set; }
    public int HealCrits { get; private set; }
    public long MaxHeal { get; private set; }
    public Dictionary<string, SkillStats> HealSkills { get; } = new(StringComparer.OrdinalIgnoreCase);

    public double CritRate => Hits == 0 ? 0 : (double)Crits / Hits;
    public double HealCritRate => HealHits == 0 ? 0 : (double)HealCrits / HealHits;

    /// <summary>Total for the given mode: damage done or HP healed.</summary>
    public long Total(MeterMode mode) => mode == MeterMode.Healing ? Healing : Damage;

    public IReadOnlyDictionary<string, SkillStats> SkillsFor(MeterMode mode) =>
        mode == MeterMode.Healing ? HealSkills : Skills;

    /// <summary>Most-voted class from the skills this combatant used.</summary>
    public AionClass DetectedClass =>
        votes.Count == 0 ? AionClass.Unknown : votes.MaxBy(v => v.Value).Key;

    internal void Add(string skill, long amount, bool critical)
    {
        Damage += amount;
        Hits++;
        if (critical) Crits++;
        if (amount > MaxHit) MaxHit = amount;
        if (!Skills.TryGetValue(skill, out var s))
            Skills[skill] = s = new SkillStats(skill);
        s.Add(amount, critical);
    }

    internal void AddHeal(string skill, long amount, bool critical)
    {
        Healing += amount;
        HealHits++;
        if (critical) HealCrits++;
        if (amount > MaxHeal) MaxHeal = amount;
        if (!HealSkills.TryGetValue(skill, out var s))
            HealSkills[skill] = s = new SkillStats(skill);
        s.Add(amount, critical);
    }

    internal void Vote(AionClass cls)
    {
        if (cls != AionClass.Unknown)
            votes[cls] = votes.GetValueOrDefault(cls) + 1;
    }
}

/// <summary>All damage since the last reset.</summary>
public sealed class Encounter
{
    /// <summary>Longer pauses than this don't count as combat time, so DPS doesn't drop while idle.</summary>
    public const double CombatGapSeconds = 15;

    readonly Dictionary<string, long> targets = new(StringComparer.OrdinalIgnoreCase);
    double activeSeconds;

    public DateTime? Start { get; private set; }
    public DateTime? End { get; private set; }
    public long DamageTaken { get; internal set; }
    public Dictionary<string, Combatant> Combatants { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Time spent in combat, in seconds (at least 1 once combat started).</summary>
    public double Seconds => Start == null ? 0 : Math.Max(1, activeSeconds);

    /// <summary>The target that took the most damage from players.</summary>
    public string? MainTarget => targets.Count == 0 ? null : targets.MaxBy(t => t.Value).Key;

    public bool IsEmpty => Combatants.Count == 0;

    internal void Advance(DateTime time)
    {
        Start ??= time;
        if (End is { } end)
        {
            var gap = (time - end).TotalSeconds;
            if (gap > 0 && gap <= CombatGapSeconds)
                activeSeconds += gap;
        }
        if (End == null || time > End)
            End = time;
    }

    /// <summary>Combatants with damage (or healing) in this mode, highest first.</summary>
    public IReadOnlyList<Combatant> Ranked(bool includeMonsters, MeterMode mode = MeterMode.Damage) =>
        Combatants.Values
            .Where(c => (includeMonsters || !c.IsMonster) && c.Total(mode) > 0)
            .OrderByDescending(c => c.Total(mode))
            .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal void AddTargetDamage(string target, long amount) =>
        targets[target] = targets.GetValueOrDefault(target) + amount;
}
