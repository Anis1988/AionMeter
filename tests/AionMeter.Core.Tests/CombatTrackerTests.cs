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
}
