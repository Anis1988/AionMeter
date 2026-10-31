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

    /// <summary>
    /// Someone used a skill without a damage number (a DoT being applied, a buff, a stun).
    /// Used to know who owns damage-over-time ticks and to detect classes.
    /// </summary>
    SkillUse,
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

    // "Lagoto inflicted 168 damage on Zoooog by reflecting the attack."
    static readonly Regex ReflectRx = new(
        Prefix + @"(?<src>.+?) (?:has )?inflicted " + Amount + @" (?:critical )?damage on (?<tgt>.+?) by reflecting the attack\.\s*$",
        Options);

    // "[Protector] received 363 damage inflicted on Npcfox by Schuka, because of the protection effect cast on it."
    static readonly Regex ProtectedRx = new(
        Prefix + @".+? received " + Amount + @" damage inflicted on (?<tgt>.+?) by (?<src>.+?), because of the protection effect.*$",
        Options);

    // "Npcfox received 26 bleeding damage after you used Lifeline Penetration."
    static readonly Regex AfterUsedRx = new(
        Prefix + @"(?<tgt>.+?) received " + Amount + @" (?:[a-z]+ )?damage after (?<src>.+?) used (?<skill>.+?)\.\s*$",
        Options);

    // "You inflicted 16,536 damage on Protectorate Searcher by using Wilderness Rage."
    // "Grop inflicted 812 critical damage on X."
    // "You inflicted 900 damage and the rune carve effect on X by using Rune Carve."
    // "Chico inflicted 663 damage on Churry and caused the Rune Carve effect by using Fang Strike."
    // "Konsa has inflicted 769 damage on you by using Gale Arrow."
    static readonly Regex HitRx = new(
        Prefix + @"(?<src>.+?) (?:has )?inflicted " + Amount + @" (?:critical )?damage(?: and .+?)? on (?<tgt>.+?)(?: and caused .+?)?(?: by using (?<skill>.+?))?\.\s*$",
        Options);

    // "Protectorate Hunter received 300 damage due to the effect of Poison Arrow."
    static readonly Regex DotRx = new(
        Prefix + @"(?<tgt>.+?) received " + Amount + @" (?:[a-z]+ )?damage due to the effect of (?<skill>.+?)\.\s*$",
        Options);

    // "Protectorate Hunter received 300 damage from Grop."
    static readonly Regex FromRx = new(
        Prefix + @"(?<tgt>.+?) received " + Amount + @" (?:critical )?damage from (?<src>.+?)\.\s*$",
        Options);

    // "Grop recovered 2,345 HP because Batte used Healing Light."
    static readonly Regex HealBecauseRx = new(
        Prefix + @"(?<tgt>.+?) recovered " + Amount + @" HP because (?<src>.+?) used (?<skill>.+?)(?: on (?:you|[\p{L}\p{Nd}]+))?\.\s*$",
        Options);

    // "You recovered 2,345 HP by using Healing Light." (a self-heal or potion)
    static readonly Regex HealByRx = new(
        Prefix + @"(?<tgt>.+?) recovered " + Amount + @" HP by using (?<skill>.+?)\.\s*$",
        Options);

    // "Grop recovered 500 HP due to the effect of Light of Rejuvenation."
    static readonly Regex HealTickRx = new(
        Prefix + @"(?<tgt>.+?) recovered " + Amount + @" HP due to the effect of (?<skill>.+?)\.\s*$",
        Options);

    // "You restored 770 of Zoooog's HP by using Draining Blow."
    // Gamez writes "You" here even when the HP owner healed themselves (potions, drains),
    // so the heal is credited to the HP owner.
    static readonly Regex RestoredOfRx = new(
        Prefix + @".+? restored " + Amount + @" of (?<tgt>.+?)'s HP(?: by using (?<skill>.+?))?\.\s*$",
        Options);

    // "Churry restored 4,300 HP." / "You recovered 1,130 HP." (potions, serums)
    static readonly Regex PlainHealRx = new(
        Prefix + @"(?<tgt>.+?) (?:restored|recovered) " + Amount + @" HP\.\s*$",
        Options);

    // "You inflicted continuous damage on Churry by using Erosion."
    static readonly Regex ContinuousRx = new(
        Prefix + @"(?<src>.+?) (?:has )?inflicted continuous damage on (?<tgt>.+?) by using (?<skill>.+?)\.\s*$",
        Options);

    // "Apexchild used Flame Cage to inflict the continuous damage effect on Ellvegas."
    static readonly Regex InflictEffectRx = new(
        Prefix + @"(?<src>.+?) used (?<skill>.+?) to inflict the continuous damage effect on (?<tgt>.+?)\.\s*$",
        Options);

    // "Troglodytes was affected by its own Enhance Magic Projectile."
    static readonly Regex OwnEffectRx = new(
        Prefix + @"(?<src>.+?) was affected by its own (?<skill>.+?)\.\s*$",
        Options);

    // "Lagoto fell down from shock because Zoooog used Wrathful Strike."
    static readonly Regex BecauseUsedRx = new(
        Prefix + @"(?<tgt>.+?) (?:is|has|fell|became|was|changed|entered)\b.*? because (?<src>.+?) used (?<skill>.+?)(?: on (?:you|[\p{L}\p{Nd}]+))?\.\s*$",
        Options);

    static readonly HashSet<string> Pronouns = new(StringComparer.OrdinalIgnoreCase) { "he", "she", "it", "they" };

    // "Weirdflexbutokay has defeated Lagoto."
    static readonly Regex DefeatedRx = new(
        Prefix + @"(?<src>.+?) has defeated (?<tgt>.+?)\.\s*$",
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

    static readonly Regex CombatNumberRx = new(@"\d[\d,]* (?:[a-z]+ )?damage|\d[\d,]* HP|'s HP", Options);

    /// <summary>
    /// True for lines that carry a damage or HP number. If <see cref="Parse"/> returns null for one
    /// of these, the meter is missing something and the line is worth reporting.
    /// </summary>
    public static bool HasCombatNumber(string line) => CombatNumberRx.IsMatch(line);

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
        if (line.Contains(" has defeated ", StringComparison.Ordinal))
        {
            var d = DefeatedRx.Match(line);
            if (d.Success && DateTime.TryParseExact(d.Groups["ts"].Value, "yyyy.MM.dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return new CombatEvent(dt, NormalizeName(d.Groups["src"].Value), NormalizeName(d.Groups["tgt"].Value),
                    0, false, null, CombatEventKind.Kill);
            return null;
        }
        if (line.Contains(" HP", StringComparison.Ordinal) && ParseHeal(line) is { } heal)
            return heal;
        if (!line.Contains(" damage", StringComparison.Ordinal))
            return ParseSkillUse(line);

        var m = ReflectRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["src"].Value, CombatEventKind.Hit) is { } reflect
                ? reflect with { Skill = "Reflect" }
                : null;

        m = ProtectedRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["src"].Value, CombatEventKind.Hit) is { } shielded
                ? shielded with { Skill = "Absorbed by protection" }
                : null;

        // Gamez often prints "after you used" for other players' bleeds and poisons,
        // so this is a tick whose owner the tracker looks up, with "you" only as a fallback.
        m = AfterUsedRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["src"].Value, CombatEventKind.DamageOverTime);

        m = HitRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["src"].Value, CombatEventKind.Hit);

        m = DotRx.Match(line);
        if (m.Success)
            return Create(m, "", CombatEventKind.DamageOverTime);

        m = FromRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["src"].Value, CombatEventKind.Hit);

        return ParseSkillUse(line);
    }

    static CombatEvent? ParseSkillUse(string line)
    {
        foreach (var rx in new[] { ContinuousRx, InflictEffectRx, OwnEffectRx, BecauseUsedRx })
        {
            var m = rx.Match(line);
            if (!m.Success)
                continue;
            var src = m.Groups["src"].Value.Trim();
            if (Pronouns.Contains(src) || !DateTime.TryParseExact(m.Groups["ts"].Value, "yyyy.MM.dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                return null;
            var target = m.Groups["tgt"].Success && rx != BecauseUsedRx ? NormalizeName(m.Groups["tgt"].Value) : "";
            return new CombatEvent(time, NormalizeName(src), target, 0, false,
                m.Groups["skill"].Value.Trim(), CombatEventKind.SkillUse);
        }
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

        m = RestoredOfRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["tgt"].Value, CombatEventKind.Heal);

        m = RestoredRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["src"].Value, CombatEventKind.Heal);

        m = PlainHealRx.Match(line);
        if (m.Success)
            return Create(m, m.Groups["tgt"].Value, CombatEventKind.Heal) is { } potion
                ? potion with { Skill = "Potion" }
                : null;

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
