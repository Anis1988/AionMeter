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
    public bool IsMonster { get; } = isMonster;
    public AionClass Class { get; internal set; }

    public long Damage { get; private set; }
    public int Hits { get; private set; }
    public int Crits { get; private set; }
    public long MaxHit { get; private set; }
    public Dictionary<string, SkillStats> Skills { get; } = new(StringComparer.OrdinalIgnoreCase);

    public double CritRate => Hits == 0 ? 0 : (double)Crits / Hits;

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

    internal void Vote(AionClass cls)
    {
        if (cls != AionClass.Unknown)
            votes[cls] = votes.GetValueOrDefault(cls) + 1;
    }
}

public sealed class Encounter(int number)
{
    readonly Dictionary<string, long> targets = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>0 for the whole-session total, otherwise the fight number.</summary>
    public int Number { get; } = number;
    public DateTime? Start { get; internal set; }
    public DateTime? End { get; internal set; }
    public long DamageTaken { get; internal set; }
    public Dictionary<string, Combatant> Combatants { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set for the session total: combat time without the idle gaps.</summary>
    internal double? FixedSeconds { get; set; }

    /// <summary>Fight length in seconds (at least 1 once combat started).</summary>
    public double Seconds =>
        FixedSeconds ?? (Start is { } s && End is { } e ? Math.Max(1, (e - s).TotalSeconds) : 0);

    /// <summary>The target that took the most damage from players.</summary>
    public string? MainTarget => targets.Count == 0 ? null : targets.MaxBy(t => t.Value).Key;

    public string Title => Number == 0
        ? "Whole session"
        : MainTarget is { } t ? $"Fight #{Number} - {t}" : $"Fight #{Number}";

    public bool IsEmpty => Combatants.Count == 0;

    /// <summary>Combatants ordered by damage, highest first.</summary>
    public IReadOnlyList<Combatant> Ranked(bool includeMonsters) =>
        Combatants.Values
            .Where(c => includeMonsters || !c.IsMonster)
            .OrderByDescending(c => c.Damage)
            .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal void AddTargetDamage(string target, long amount) =>
        targets[target] = targets.GetValueOrDefault(target) + amount;
}
