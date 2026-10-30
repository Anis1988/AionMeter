using AionMeter.Core;

namespace AionMeter.Core.Tests;

public class CombatTrackerTests
{
    static CombatTracker Run(params string[] lines) => Run(new TrackerOptions { YourName = "Batte" }, lines);

    static CombatTracker Run(TrackerOptions options, params string[] lines)
    {
        var tracker = new CombatTracker(options);
        foreach (var line in lines)
            if (LogParser.Parse(line) is { } e)
                tracker.Process(e);
        return tracker;
    }

    [Fact]
    public void RanksPlayersByDamage()
    {
        var t = Run(
            "2026.10.28 17:02:10 : Grop inflicted 2,978 damage on Protectorate Hunter by using Dazing Severe Blow.",
            "2026.10.28 17:02:11 : You inflicted 5,000 damage on Protectorate Hunter by using Wilderness Rage.",
            "2026.10.28 17:02:12 : Lumi inflicted 100 damage on Protectorate Hunter.");

        var ranked = t.Current.Ranked(includeMonsters: false);
        Assert.Equal(["Batte", "Grop", "Lumi"], ranked.Select(c => c.DisplayName));
        Assert.True(ranked[0].IsYou);
        Assert.Equal("Protectorate Hunter", t.Current.MainTarget);
    }

    [Fact]
    public void SeparatesMonstersAndCountsDamageTaken()
    {
        var t = Run(
            "2026.10.28 17:02:10 : You inflicted 1,000 damage on Protectorate Hunter.",
            "2026.10.28 17:02:11 : Protectorate Hunter inflicted 640 damage on you.");

        Assert.Single(t.Current.Ranked(includeMonsters: false));
        Assert.Equal(2, t.Current.Ranked(includeMonsters: true).Count);
        Assert.Equal(640, t.Current.DamageTaken);
    }

    [Fact]
    public void DetectsClassFromSkills()
    {
        var t = Run(
            "2026.10.28 17:02:10 : Grop inflicted 2,978 damage on Mob A by using Divine Blow.",
            "2026.10.28 17:02:11 : Zen inflicted 1,000 damage on Mob A by using Flame Bolt VI.");

        Assert.Equal(AionClass.Templar, t.Current.Combatants["Grop"].Class);
        Assert.Equal(AionClass.Sorcerer, t.Current.Combatants["Zen"].Class);
    }

    [Fact]
    public void LearnsSkillsFromYourClass()
    {
        var t = Run(new TrackerOptions { YourName = "Batte", YourClass = AionClass.Ranger },
            "2026.10.28 17:02:10 : You inflicted 1,000 damage on Mob A by using Wilderness Rage.",
            "2026.10.28 17:02:11 : Kira inflicted 1,000 damage on Mob A by using Wilderness Rage II.");

        Assert.Equal(AionClass.Ranger, t.Current.Combatants[LogParser.You].Class);
        Assert.Equal(AionClass.Ranger, t.Current.Combatants["Kira"].Class);
        Assert.True(t.LearnedSomething);
    }

    [Fact]
    public void ManualClassOverridesDetection()
    {
        var t = Run("2026.10.28 17:02:10 : Grop inflicted 2,978 damage on Mob A by using Divine Blow.");

        t.SetClass("Grop", AionClass.Gladiator);
        Assert.Equal(AionClass.Gladiator, t.Current.Combatants["Grop"].Class);

        t.SetClass("Grop", AionClass.Unknown);
        Assert.Equal(AionClass.Templar, t.Current.Combatants["Grop"].Class);
    }

    [Fact]
    public void CreditsDotTicksToWhoAppliedThem()
    {
        var t = Run(
            "2026.10.28 17:02:10 : You inflicted 500 damage on Mob A by using Poison Arrow.",
            "2026.10.28 17:02:11 : Mob A received 300 damage due to the effect of Poison Arrow.",
            "2026.10.28 17:02:12 : Mob A received 300 damage due to the effect of Unknown Curse.");

        Assert.Equal(800, t.Current.Combatants[LogParser.You].Damage);
        Assert.Equal(300, t.Current.Combatants[CombatTracker.UnknownDotName].Damage);
    }

    [Fact]
    public void KeepsCountingUntilReset()
    {
        var t = Run(
            "2026.10.28 17:00:00 : You inflicted 1,000 damage on Mob A.",
            "2026.10.28 17:00:05 : You inflicted 1,000 damage on Mob A.",
            "2026.10.28 17:30:00 : You inflicted 500 damage on Mob B.",
            "2026.10.28 17:30:03 : You inflicted 500 damage on Mob B.");

        Assert.Equal(3000, t.Current.Combatants[LogParser.You].Damage);
        // The 30-minute break doesn't count as combat time.
        Assert.Equal(8, t.Current.Seconds);

        t.Reset();
        Assert.True(t.Current.IsEmpty);
        Assert.Equal(0, t.Current.Seconds);
    }

    [Fact]
    public void MergesYourSummonsWhenYouAreSpiritmaster()
    {
        var t = Run(new TrackerOptions { YourName = "Batte", YourClass = AionClass.Spiritmaster },
            "2026.10.28 17:00:00 : You inflicted 1,000 damage on Mob A by using Erosion.",
            "2026.10.28 17:00:01 : Fire Spirit inflicted 400 damage on Mob A.");

        var me = t.Current.Combatants[LogParser.You];
        Assert.Equal(1400, me.Damage);
        Assert.Contains("Fire Spirit", me.Skills.Keys);
    }

    [Fact]
    public void CreditsNamedPetsToOwner()
    {
        var t = Run("2026.10.28 17:00:00 : Grop's Fire Spirit inflicted 400 damage on Mob A.");

        Assert.Equal(400, t.Current.Combatants["Grop"].Damage);
    }

    [Fact]
    public void TracksHealingSeparatelyFromDamage()
    {
        var t = Run(
            "2026.10.28 17:00:00 : You inflicted 1,000 damage on Mob A.",
            "2026.10.28 17:00:01 : You recovered 2,000 HP because Lumi used Healing Light.",
            "2026.10.28 17:00:02 : Grop recovered 500 HP because Lumi used Light of Recovery.",
            "2026.10.28 17:00:03 : You recovered 300 HP by using Recovery Spell.");

        var healers = t.Current.Ranked(includeMonsters: false, MeterMode.Healing);
        Assert.Equal(["Lumi", "Batte"], healers.Select(c => c.DisplayName));
        Assert.Equal(2500, healers[0].Healing);
        Assert.Equal(AionClass.Cleric, healers[0].Class);

        // Lumi only healed, so she isn't in the damage ranking.
        Assert.Equal(["Batte"], t.Current.Ranked(includeMonsters: false).Select(c => c.DisplayName));
    }

    [Fact]
    public void CreditsHealOverTimeToWhoCastIt()
    {
        var t = Run(
            "2026.10.28 17:00:00 : Grop recovered 1,000 HP because Lumi used Light of Rejuvenation.",
            "2026.10.28 17:00:01 : Grop recovered 200 HP due to the effect of Light of Rejuvenation.",
            "2026.10.28 17:00:02 : Grop recovered 50 HP due to the effect of Mystery Regen.");

        Assert.Equal(1200, t.Current.Combatants["Lumi"].Healing);
        Assert.Equal(50, t.Current.Combatants[CombatTracker.UnknownHotName].Healing);
    }

    [Fact]
    public void IgnoresMonsterHeals()
    {
        var t = Run("2026.10.28 17:00:00 : Protectorate Hunter recovered 1,000 HP by using Regeneration.");

        Assert.True(t.Current.IsEmpty);
    }

    [Fact]
    public void DetectsBossFromDamageTakenInOneLife()
    {
        var t = Run(new TrackerOptions { YourName = "Batte", BossDamageThreshold = 10_000 },
            "2026.10.28 17:00:00 : Bakarma Guardian inflicted 500 damage on you.",
            "2026.10.28 17:00:01 : You inflicted 6,000 damage on Bakarma Guardian.",
            "2026.10.28 17:00:02 : You inflicted 6,000 damage on Bakarma Guardian.");

        Assert.True(t.Current.Combatants["Bakarma Guardian"].IsBoss);
    }

    [Fact]
    public void KillsResetBossDamage()
    {
        var t = Run(new TrackerOptions { YourName = "Batte", BossDamageThreshold = 10_000 },
            "2026.10.28 17:00:00 : Protectorate Hunter inflicted 500 damage on you.",
            "2026.10.28 17:00:01 : You inflicted 6,000 damage on Protectorate Hunter.",
            "2026.10.28 17:00:02 : You have gained 1,000 XP from Protectorate Hunter.",
            "2026.10.28 17:00:03 : You inflicted 6,000 damage on Protectorate Hunter.");

        Assert.False(t.Current.Combatants["Protectorate Hunter"].IsBoss);
    }

    [Fact]
    public void MarkedBossIsTreatedAsMonster()
    {
        var t = Run("2026.10.28 17:00:00 : Kromede inflicted 500 damage on you.");
        Assert.False(t.Current.Combatants["Kromede"].IsMonster);

        t.SetBoss("Kromede", true);
        var kromede = t.Current.Combatants["Kromede"];
        Assert.True(kromede.IsBoss);
        Assert.True(kromede.IsMonster);
    }
}
