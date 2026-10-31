using System.Text.RegularExpressions;
using AionMeter.Core;

namespace AionMeter.Core.Tests;

/// <summary>Checks the meter against a real Gamez Aion 4.8 siege log.</summary>
public class GamezSiegeLogTests
{
    static readonly string[] Lines =
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Logs", "gamez-siege.log"));

    static CombatTracker Run()
    {
        var t = new CombatTracker(new TrackerOptions { YourName = "Batte", YourClass = AionClass.Spiritmaster });
        foreach (var line in Lines)
            if (LogParser.Parse(line) is { } e)
                t.Process(e);
        return t;
    }

    [Fact]
    public void EveryDamageAndHealLineIsRecognised()
    {
        var amount = new Regex(@"\d[\d,]* (?:[a-z]+ )?damage|\d[\d,]* HP|'s HP");
        var missed = Lines.Where(l => amount.IsMatch(l) && LogParser.Parse(l) == null).ToList();
        Assert.Empty(missed);
    }

    [Fact]
    public void RuneCarveLinesKeepTheRealTarget()
    {
        var e = LogParser.Parse("2026.10.30 20:14:39 : Chicopicadinho inflicted 663 damage on Churry and caused the Rune Carve effect by using Fang Strike.");

        Assert.NotNull(e);
        Assert.Equal("Churry", e.Target);
        Assert.Equal("Fang Strike", e.Skill);
    }

    [Fact]
    public void CountsDamageDoneToYou()
    {
        // 769 + 732 + 1,385 (taken by your spirit's protection) + 1,036 + 743
        Assert.Equal(4665, Run().Current.DamageTaken);
    }

    [Fact]
    public void CreditsTicksToWhoeverUsedTheSkill()
    {
        var c = Run().Current.Combatants;

        // Your Erosion hits plus its 289 tick.
        Assert.Equal(756 + 249 + 289, c[LogParser.You].Damage);
        // Gamez says "after you used Lifeline Penetration", but it was Troglodytes'.
        // "Effect Enhance Magic Projectile" ticks are Troglodytes' too.
        Assert.Equal(1136 + 4110 + 4895 + 2542 + 670 + 11439 + 73 + 73 + 296, c["Troglodytes"].Damage);
    }

    [Fact]
    public void CountsPotionsSummonsAndTargetedHeals()
    {
        var c = Run().Current.Combatants;

        Assert.Equal(4797 + 209, c["Rosh"].Healing);
        Assert.Contains("Prayer of Resilience", c["Rosh"].HealSkills.Keys);
        Assert.Equal(446 + 446 + 208, c["Siriuus"].Healing);
        Assert.Equal(4300 + 623, c["Churry"].Healing);
        Assert.Equal(1130, c[LogParser.You].Healing);
        Assert.DoesNotContain("Healing Servant", c.Keys);
    }

    [Fact]
    public void TracksDamagePerTarget()
    {
        var enc = Run().Current;

        Assert.Equal(2291 + 3150 + 1620 + 849, enc.Combatants["Qoooqooo"].DamageOn("Ellvegas"));
        Assert.True(enc.Target("Protectorate Special Enforcer")!.Damage > 0);
        Assert.Contains(enc.Targets, t => t.Name == "Protectorate Pathfinder");
    }

    [Theory]
    [InlineData("Troglodytes", AionClass.Gunner)]
    [InlineData("Qoooqooo", AionClass.Ranger)]
    [InlineData("Chicopicadinho", AionClass.Assassin)]
    [InlineData("Jetta", AionClass.Songweaver)]
    [InlineData("Rosh", AionClass.Templar)]
    [InlineData("Hanz", AionClass.Gladiator)]
    [InlineData("Huahahahahaha", AionClass.Aethertech)]
    [InlineData("Siriuus", AionClass.Cleric)]
    public void DetectsClasses(string name, AionClass cls) =>
        Assert.Equal(cls, Run().Current.Combatants[name].Class);
}
