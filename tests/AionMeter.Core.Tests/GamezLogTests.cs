using System.Text.RegularExpressions;
using AionMeter.Core;

namespace AionMeter.Core.Tests;

/// <summary>Checks the meter against a real Gamez Aion 4.8 PvP log.</summary>
public class GamezLogTests
{
    static readonly string[] Lines =
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Logs", "gamez-pvp.log"));

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
    public void DamageTotalsMatchTheLog()
    {
        var c = Run().Current.Combatants;

        Assert.Equal(4455, c["Zoooog"].Damage);
        Assert.Equal(7984, c["Weirdflexbutokay"].Damage);
        Assert.Equal(6079, c["Mariadopvp"].Damage);
        Assert.Equal(3084, c["Lowest"].Damage);
        Assert.Equal(15 * 168, c["Lagoto"].Damage);
        Assert.Equal(1311, c["Schuka"].Damage);
        Assert.Equal(26, c[LogParser.You].Damage);
        Assert.DoesNotContain(c.Keys, k => k.Contains("reflecting") || k.Contains("Protector"));
    }

    [Fact]
    public void HealingTotalsMatchTheLog()
    {
        var c = Run().Current.Combatants;

        Assert.Equal(3276, c["Hizie"].Healing);
        Assert.Equal(770, c["Zoooog"].Healing);
        Assert.Equal(744, c["Lagoto"].Healing);
        Assert.Equal(385, c["Schuka"].Healing);
        Assert.Equal(985, c[LogParser.You].Healing);
        Assert.DoesNotContain("Earth Spirit", c.Keys);
    }

    [Fact]
    public void DetectsClassesFromSkills()
    {
        var c = Run().Current.Combatants;

        Assert.Equal(AionClass.Gladiator, c["Zoooog"].Class);
        Assert.Equal(AionClass.Assassin, c["Weirdflexbutokay"].Class);
        Assert.Equal(AionClass.Sorcerer, c["Mariadopvp"].Class);
        Assert.Equal(AionClass.Songweaver, c["Lowest"].Class);
        Assert.Equal(AionClass.Gunner, c["Schuka"].Class);
        Assert.Equal(AionClass.Cleric, c["Hizie"].Class);
    }
}
