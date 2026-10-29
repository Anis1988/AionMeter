using System.Text.RegularExpressions;

namespace AionMeter.Core;

/// <summary>Maps skill names to classes, from built-in data plus skills learned at runtime.</summary>
public sealed class ClassDetector
{
    static readonly Regex RankSuffix = new(@"\s+(?:[IVX]+|\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    readonly Dictionary<string, AionClass> builtIn = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, AionClass> learned = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> ambiguous = new(StringComparer.OrdinalIgnoreCase);

    public ClassDetector(IEnumerable<KeyValuePair<string, AionClass>>? learnedSkills = null)
    {
        foreach (var (cls, skills) in SkillData.Skills)
            foreach (var skill in skills)
                builtIn[Normalize(skill)] = cls;

        if (learnedSkills != null)
            foreach (var (skill, cls) in learnedSkills)
                if (cls != AionClass.Unknown && !builtIn.ContainsKey(Normalize(skill)))
                    learned[Normalize(skill)] = cls;
    }

    /// <summary>Skills learned so far (to be saved between runs).</summary>
    public IReadOnlyDictionary<string, AionClass> Learned => learned;

    /// <summary>Strips the rank suffix: "Flame Bolt VI" → "Flame Bolt".</summary>
    public static string Normalize(string skill) => RankSuffix.Replace(skill.Trim(), "");

    public AionClass Lookup(string? skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return AionClass.Unknown;
        var key = Normalize(skill);
        if (builtIn.TryGetValue(key, out var cls) || learned.TryGetValue(key, out cls))
            return cls;
        return AionClass.Unknown;
    }

    /// <summary>
    /// Records that <paramref name="cls"/> used <paramref name="skill"/>. A skill seen
    /// with two different classes (item or shared skills) is never used for detection.
    /// </summary>
    public bool Learn(string? skill, AionClass cls)
    {
        if (string.IsNullOrWhiteSpace(skill) || cls == AionClass.Unknown)
            return false;
        var key = Normalize(skill);
        if (builtIn.ContainsKey(key) || ambiguous.Contains(key))
            return false;
        if (learned.TryGetValue(key, out var existing))
        {
            if (existing == cls)
                return false;
            learned.Remove(key);
            ambiguous.Add(key);
            return true;
        }
        learned[key] = cls;
        return true;
    }

    public static AionClass SummonClass(string name) =>
        SkillData.Summons.TryGetValue(name, out var cls) ? cls : AionClass.Unknown;
}
