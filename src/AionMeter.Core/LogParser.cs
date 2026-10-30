using System.Globalization;
using System.Text.RegularExpressions;

namespace AionMeter.Core;

public enum CombatEventKind
{
    /// <summary>A hit with a known source.</summary>
    Hit,

    /// <summary>A damage-over-time tick; the log doesn't name the source.</summary>
    DamageOverTime,

    /// <summary>HP restored by a skill or item with a known healer.</summary>
    Heal,

    /// <summary>A heal-over-time tick; the log doesn't name the healer.</summary>
    HealOverTime,

    /// <summary>A monster died (you got XP for it). Amount is the XP.</summary>
    Kill,

    /// <summary>Someone summoned something; Source is the summoner and Target the summon.</summary>
    Summon,
}

/// <summary>What the meter ranks players by.</summary>
public enum MeterMode
{
    Damage,
    Healing,
}

public sealed record CombatEvent(
    DateTime Time,
    string Source,
    string Target,
    long Amount,
    bool Critical,
    string? Skill,
    CombatEventKind Kind);

/// <summary>Parses English Aion 4.x Chat.log damage and healing lines.</summary>
public static class LogParser
{
    /// <summary>Name used for the player who owns the log.</summary>
    public const string You = "You";

    const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    // "2026.10.28 17:03:23 : Critical Strike!" (the crit prefix is optional)
    const string Prefix = @"^(?<ts>\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2}) : (?<crit>Critical [A-Za-z]+(?: [A-Za-z]+)?!\s*)?";
    const string Amount = @"(?<dmg>\d[\d,.]*)";

    // "You inflicted 16,536 damage on Protectorate Searcher by using Wilderness Rage."
    // "Grop inflicted 812 critical damage on X."
    // "You inflicted 900 damage and the rune carve effect on X by using Rune Carve."
    static readonly Regex HitRx = new(
        Prefix + @"(?<src>.+?) inflicted " + Amount + @" (?:critical )?damage(?: and .+?)? on (?<tgt>.+?)(?: by using (?<skill>.+?))?\.\s*$",
        Options);

    // "Protectorate Hunter received 300 damage due to the effect of Poison Arrow."
    static readonly Regex DotRx = new(
        Prefix + @"(?<tgt>.+?) received " + Amount + @" (?:critical )?damage due to the effect of (?<skill>.+?)\.\s*$",
        Options);

    // "Protectorate Hunter received 300 damage from Grop."
    static readonly Regex FromRx = new(
        Prefix + @"(?<tgt>.+?) received " + Amount + @" (?:critical )?damage from (?<src>.+?)\.\s*$",
        Options);

    // "Grop recovered 2,345 HP because Batte used Healing Light."
    static readonly Regex HealBecauseRx = new(
        Prefix + @"(?<tgt>.+?) recovered " + Amount + @" HP because (?<src>.+?) used (?<skill>.+?)\.\s*$",
        Options);

    // "You recovered 2,345 HP by using Healing Light." (a self-heal or potion)
    static readonly Regex HealByRx = new(
        Prefix + @"(?<tgt>.+?) recovered " + Amount + @" HP by using (?<skill>.+?)\.\s*$",
        Options);

    // "Grop recovered 500 HP due to the effect of Light of Rejuvenation."
    static readonly Regex HealTickRx = new(
        Prefix + @"(?<tgt>.+?) recovered " + Amount + @" HP due to the effect of (?<skill>.+?)\.\s*$",
        Options);

    // "You restored 2,345 HP of Grop by using Healing Light."
    static readonly Regex RestoredRx = new(
        Prefix + @"(?<src>.+?) restored " + Amount + @" HP (?:of|to) (?<tgt>.+?)(?: by using (?<skill>.+?))?\.\s*$",
        Options);

    // "You have gained 825,736 XP from Protectorate Searcher (Energy of Repose 235,924)."
    static readonly Regex KillRx = new(
        Prefix + @"You have gained " + Amount + @" XP from (?<tgt>.+?)(?: \(.*\))?\.\s*$",
        Options);

    // "You summoned Cyclone Servant by using Summon Cyclone Servant." / "Grop has summoned Fire Spirit."
    static readonly Regex SummonRx = new(
        Prefix + @"(?<src>.+?) (?:have |has )?summoned (?:an? |the )?(?<tgt>.+?)(?: by using (?<skill>.+?))?\.\s*$",
        Options);

    public static CombatEvent? Parse(string line)
    {
        if (string.IsNullOrEmpty(line))
            return null;
        if (line.Contains(" summoned ", StringComparison.Ordinal))
        {
            var s = SummonRx.Match(line);
            if (s.Success && DateTime.TryParseExact(s.Groups["ts"].Value, "yyyy.MM.dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
                return new CombatEvent(t, NormalizeName(s.Groups["src"].Value), s.Groups["tgt"].Value.Trim(), 0,
                    false, s.Groups["skill"].Success ? s.Groups["skill"].Value.Trim() : null, CombatEventKind.Summon);
        }
        if (line.Contains(" XP from ", StringComparison.Ordinal))
        {
            var k = KillRx.Match(line);
            return k.Success ? Create(k, "", CombatEventKind.Kill) : null;
        }
        if (line.Contains(" HP", StringComparison.Ordinal) && ParseHeal(line) is { } heal)
            return heal;
        if (!line.Contains(" damage", StringComparison.Ordinal))
            return null;

        var m = HitRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["src"].Value, CombatEventKind.Hit);

        m = DotRx.Match(line);
        if (m.Success)
            return Create(m, "", CombatEventKind.DamageOverTime);

        m = FromRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["src"].Value, CombatEventKind.Hit);

        return null;
    }

    static CombatEvent? ParseHeal(string line)
    {
        var m = HealBecauseRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["src"].Value, CombatEventKind.Heal);

        m = HealTickRx.Match(line);
        if (m.Success)
            return Create(m, "", CombatEventKind.HealOverTime);

        m = HealByRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["tgt"].Value, CombatEventKind.Heal);

        m = RestoredRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["src"].Value, CombatEventKind.Heal);

        return null;
    }

    static CombatEvent? Create(Match m, string source, CombatEventKind kind)
    {
        if (!DateTime.TryParseExact(m.Groups["ts"].Value, "yyyy.MM.dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return null;

        var digits = new string(m.Groups["dmg"].Value.Where(char.IsAsciiDigit).ToArray());
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
            return null;

        var skill = m.Groups["skill"].Success ? m.Groups["skill"].Value.Trim() : null;
        return new CombatEvent(
            time,
            NormalizeName(source),
            NormalizeName(m.Groups["tgt"].Value),
            amount,
            m.Groups["crit"].Success,
            string.IsNullOrEmpty(skill) ? null : skill,
            kind);
    }

    static string NormalizeName(string name)
    {
        name = name.Trim();
        return name.Equals("you", StringComparison.OrdinalIgnoreCase) ? You : name;
    }
}
