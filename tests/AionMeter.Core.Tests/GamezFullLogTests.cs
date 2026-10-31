using System.Text.RegularExpressions;
using AionMeter.Core;

namespace AionMeter.Core.Tests;

/// <summary>
/// The whole Gamez siege log, checked line by line: every damage and heal line is read, and totals
/// match a plain count of the "inflicted N damage" lines plus the ticks and hits that count doesn't see.
/// </summary>
public class GamezFullLogTests
{
    static readonly string[] Lines =
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Logs", "gamez-siege-full.log"));

    static readonly Encounter Fight = Run();

    static Encounter Run()
    {
        var t = new CombatTracker(new TrackerOptions { YourName = "Batte", YourClass = AionClass.Spiritmaster });
        foreach (var line in Lines)
            if (LogParser.Parse(line) is { } e)
                t.Process(e);
        return t.Current;
    }

    [Fact]
    public void EveryDamageAndHealLineIsRead()
    {
        var amount = new Regex(@"\d[\d,]* (?:[a-z]+ )?damage|\d[\d,]* HP|'s HP");
        Assert.Empty(Lines.Where(l => amount.IsMatch(l) && LogParser.Parse(l) == null));
    }

    [Theory]
    // Direct hits only.
    [InlineData("Thorium", 25759)]
    [InlineData("Hanz", 21053)]
    [InlineData("Slayer", 18711)]
    [InlineData("Qoooqooo", 15635)]
    [InlineData("Qooooqoooo", 12579)]
    [InlineData("Chicopicadinho", 7763)]
    [InlineData("Jetta", 7086)]
    // Hits + Enhance Magic Projectile (670 + 296), Promise of Magic Power (127 + 134) and bleed (73 + 73) ticks.
    [InlineData("Troglodytes", 33101 + 670 + 296 + 127 + 134 + 73 + 73)]
    // Hits + "You received N damage from Konsa" (1,044 + 743 + 724).
    [InlineData("Konsa", 24195 + 1044 + 743 + 724)]
    // Hits + "You received 1,036 damage from Blaz".
    [InlineData("Blaz", 16130 + 1036)]
    // Your Erosion hits + five 289 ticks.
    [InlineData(LogParser.You, 756 + 249 + 5 * 289)]
    public void DamageMatchesTheLog(string name, long damage) =>
        Assert.Equal(damage, Fight.Combatants[name].Damage);

    [Fact]
    public void TicksWithNoKnownOwnerAreNotGivenToAnyone()
    {
        // "Air Change Effect" ticks on players have no owner in the log, and
        // "after you used Agonising Arrow" isn't you (a Spiritmaster).
        Assert.DoesNotContain(CombatTracker.UnknownDotName, Fight.Combatants.Keys);
        Assert.DoesNotContain(Fight.Combatants[LogParser.You].Skills.Keys, k => k.Contains("Agonising"));
    }
}
