using System.Globalization;
using System.Text.RegularExpressions;

namespace AionMeter.Core;

public enum CombatEventKind
{
    /// <summary>A hit with a known source.</summary>
    Hit,

    /// <summary>A damage-over-time tick; the log doesn't name the source.</summary>
    DamageOverTime,
}

public sealed record CombatEvent(
    DateTime Time,
    string Source,
    string Target,
    long Amount,
    bool Critical,
    string? Skill,
    CombatEventKind Kind);

/// <summary>Parses English Aion 4.x Chat.log combat lines.</summary>
public static class LogParser
{
    /// <summary>Name used for the player who owns the log.</summary>
    public const string You = "You";

    const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    // "2026.10.28 17:03:23 : Critical Strike!" (the crit prefix is optional)
    const string Prefix = @"^(?<ts>\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2}) : (?<crit>Critical (?:Strike|Hit)!\s*)?";
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

    public static CombatEvent? Parse(string line)
    {
        if (string.IsNullOrEmpty(line) || !line.Contains(" damage", StringComparison.Ordinal))
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
