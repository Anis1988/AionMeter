namespace AionMeter.Core;

/// <summary>
/// Built-in skill → class hints used to guess other players' classes.
/// Names are matched without their rank suffix ("Flame Bolt VI" → "Flame Bolt").
/// The list is best-effort: skills it doesn't know are learned from your own
/// class and from classes you set by hand, and saved with your settings.
/// </summary>
public static class SkillData
{
    public static IReadOnlyDictionary<AionClass, string[]> Skills { get; } = new Dictionary<AionClass, string[]>
    {
        [AionClass.Gladiator] =
        [
            "Ferocious Strike", "Robust Blow", "Body Smash", "Weakening Severe Blow", "Seismic Wave",
            "Seismic Billow", "Pressure Wave", "Sharp Strike", "Crashing Wind Strike", "Tendon Slice",
            "Great Cleave", "Piercing Rupture", "Cleave", "Wrathful Strike", "Shock Wave",
        ],
        [AionClass.Templar] =
        [
            "Divine Blow", "Dazing Severe Blow", "Shining Slash", "Holy Punishment", "Judgment",
            "Punishment", "Righteous Cleave", "Provoking Shield Counter", "Shield Counter", "Face Smash",
            "Inquisitor's Blow", "Empyrean Fury", "Divine Fury", "Doom Lure",
        ],
        [AionClass.Assassin] =
        [
            "Swift Edge", "Fang Strike", "Soul Slash", "Surprise Attack", "Ambush", "Assassination",
            "Killing Spree", "Whirlwind Slash", "Lightning Slash", "Rune Carve", "Pain Rune", "Rune Knife",
            "Beast Kick", "Beast Swipe", "Sigil Strike", "Blood Rune", "Heart Gore", "Shadowfall",
            "Apply Poison", "Apply Deadly Poison",
        ],
        [AionClass.Ranger] =
        [
            "Swift Shot", "Aimed Shot", "Rupture Arrow", "Arrow Strike", "Spiral Arrow", "Arrow Storm",
            "Heart Shot", "Gale Arrow", "Shock Arrow", "Seizure Arrow", "Stunning Shot", "Lightning Arrow",
            "Arrow Deluge", "Silence Arrow", "Retreating Shot", "Deadshot", "Entangling Shot", "Poison Arrow",
            "Explosive Trap", "Spike Trap", "Thunderbolt Trap", "Sandstorm Trap",
        ],
        [AionClass.Sorcerer] =
        [
            "Flame Bolt", "Flame Spray", "Flame Cage", "Flame Harpoon", "Flame Fusion", "Ice Chain",
            "Ice Harpoon", "Glacial Shard", "Frozen Shock", "Blaze", "Storm Strike", "Wind Cut Down",
            "Winter Binding", "Lumiel's Wrath", "Cyclone Strike", "Delayed Blast", "Soul Freeze",
            "Aether's Hold", "Infernal Blaze",
        ],
        [AionClass.Spiritmaster] =
        [
            "Erosion", "Spirit Erosion", "Fear Shriek", "Chain of Earth", "Cyclone of Wrath", "Absorb Energy",
            "Ignite Aether", "Sandblaster", "Cursecloud", "Magic Implosion", "Spirit Detonation",
            "Infernal Blight",
        ],
        [AionClass.Cleric] =
        [
            "Smite", "Chastise", "Earth's Wrath", "Divine Spark", "Enfeebling Burst",
            "Healing Light", "Light of Recovery", "Radiant Cure", "Healing Wind", "Light of Rejuvenation",
            "Flash of Recovery", "Splendor of Recovery",
        ],
        [AionClass.Chanter] =
        [
            "Meteor Strike", "Booming Strike", "Booming Smash", "Booming Assault", "Parrying Strike",
            "Incandescent Blow", "Soul Crush", "Disorienting Blow", "Inescapable Judgment", "Pentacle Shock",
            "Mountain Crash", "Recovery Spell", "Healing Conduit", "Word of Revival",
        ],
        [AionClass.Gunner] = [],
        [AionClass.Songweaver] = [],
        [AionClass.Aethertech] = [],
    };

    /// <summary>Summons whose damage shows up under their own name.</summary>
    public static IReadOnlyDictionary<string, AionClass> Summons { get; } =
        new Dictionary<string, AionClass>(StringComparer.OrdinalIgnoreCase)
        {
            ["Fire Spirit"] = AionClass.Spiritmaster,
            ["Water Spirit"] = AionClass.Spiritmaster,
            ["Earth Spirit"] = AionClass.Spiritmaster,
            ["Wind Spirit"] = AionClass.Spiritmaster,
            ["Magma Spirit"] = AionClass.Spiritmaster,
            ["Tempest Spirit"] = AionClass.Spiritmaster,
            ["Holy Servant"] = AionClass.Cleric,
        };
}
