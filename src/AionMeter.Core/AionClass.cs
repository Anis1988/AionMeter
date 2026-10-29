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
        AionClass.Gladiator => "#C0392B",
        AionClass.Templar => "#D4AC2B",
        AionClass.Assassin => "#A0306A",
        AionClass.Ranger => "#3FA34D",
        AionClass.Sorcerer => "#2F7FE0",
        AionClass.Spiritmaster => "#8E5CE8",
        AionClass.Cleric => "#C9C2A0",
        AionClass.Chanter => "#1FA595",
        AionClass.Gunner => "#E07B24",
        AionClass.Songweaver => "#E05FA8",
        AionClass.Aethertech => "#6F8496",
        _ => "#5A6270",
    };
}
