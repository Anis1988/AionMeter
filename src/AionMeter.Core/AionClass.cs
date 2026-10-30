namespace AionMeter.Core;

/// <summary>Playable classes in Aion 4.8.</summary>
public enum AionClass
{
    Unknown,
    Gladiator,
    Templar,
    Assassin,
    Ranger,
    Sorcerer,
    Spiritmaster,
    Cleric,
    Chanter,
    Gunner,
    Songweaver,
    Aethertech,
}

public static class AionClassInfo
{
    public static IReadOnlyList<AionClass> Playable { get; } =
        Enum.GetValues<AionClass>().Where(c => c != AionClass.Unknown).ToArray();

    public static string ShortName(AionClass c) => c switch
    {
        AionClass.Gladiator => "GL",
        AionClass.Templar => "TE",
        AionClass.Assassin => "AS",
        AionClass.Ranger => "RA",
        AionClass.Sorcerer => "SO",
        AionClass.Spiritmaster => "SM",
        AionClass.Cleric => "CL",
        AionClass.Chanter => "CH",
        AionClass.Gunner => "GU",
        AionClass.Songweaver => "SW",
        AionClass.Aethertech => "AT",
        _ => "?",
    };

    /// <summary>Bar/icon colour as #RRGGBB.</summary>
    public static string Color(AionClass c) => c switch
    {
        AionClass.Gladiator => "#22C55E",
        AionClass.Templar => "#3B82F6",
        AionClass.Assassin => "#A855F7",
        AionClass.Ranger => "#14B8A6",
        AionClass.Sorcerer => "#F97316",
        AionClass.Spiritmaster => "#6366F1",
        AionClass.Cleric => "#F5E6B8",
        AionClass.Chanter => "#EAB308",
        AionClass.Gunner => "#EF4444",
        AionClass.Songweaver => "#EC4899",
        AionClass.Aethertech => "#94A3B8",
        _ => "#64748B",
    };
}
