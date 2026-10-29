using AionMeter.Core;

namespace AionMeter.Core.Tests;

public class LogParserTests
{
    [Fact]
    public void ParsesOwnCriticalSkillHit()
    {
        var e = LogParser.Parse("2026.10.28 17:03:23 : Critical Strike!You inflicted 16,536 damage on Protectorate Searcher by using Wilderness Rage. ");

        Assert.NotNull(e);
        Assert.Equal(new DateTime(2026, 10, 28, 17, 3, 23), e.Time);
        Assert.Equal(LogParser.You, e.Source);
        Assert.Equal("Protectorate Searcher", e.Target);
        Assert.Equal(16536, e.Amount);
        Assert.True(e.Critical);
        Assert.Equal("Wilderness Rage", e.Skill);
        Assert.Equal(CombatEventKind.Hit, e.Kind);
    }

    [Fact]
    public void ParsesOtherPlayerHit()
    {
        var e = LogParser.Parse("2026.10.28 17:02:10 : Critical Strike!Grop inflicted 2,978 damage on Protectorate Hunter by using Dazing Severe Blow. ");

        Assert.NotNull(e);
        Assert.Equal("Grop", e.Source);
        Assert.Equal(2978, e.Amount);
        Assert.Equal("Dazing Severe Blow", e.Skill);
    }

    [Fact]
    public void ParsesAutoAttackWithoutSkill()
    {
        var e = LogParser.Parse("2026.10.28 17:02:12 : You inflicted 1,204 damage on Protectorate Hunter.");

        Assert.NotNull(e);
        Assert.Null(e.Skill);
        Assert.False(e.Critical);
        Assert.Equal("Protectorate Hunter", e.Target);
    }

    [Fact]
    public void ParsesCriticalHitVariant()
    {
        var e = LogParser.Parse("2026.10.28 17:02:12 : Critical Hit! You inflicted 3,000 critical damage on Mob.");

        Assert.NotNull(e);
        Assert.True(e.Critical);
        Assert.Equal(3000, e.Amount);
        Assert.Equal("Mob", e.Target);
    }

    [Fact]
    public void ParsesDamageWithExtraEffect()
    {
        var e = LogParser.Parse("2026.10.28 17:02:12 : You inflicted 900 damage and the rune carve effect on Training Dummy by using Rune Carve.");

        Assert.NotNull(e);
        Assert.Equal("Training Dummy", e.Target);
        Assert.Equal("Rune Carve", e.Skill);
    }

    [Fact]
    public void ParsesDamageOverTime()
    {
        var e = LogParser.Parse("2026.10.28 17:02:14 : Protectorate Hunter received 300 damage due to the effect of Poison Arrow.");

        Assert.NotNull(e);
        Assert.Equal(CombatEventKind.DamageOverTime, e.Kind);
        Assert.Equal("Protectorate Hunter", e.Target);
        Assert.Equal("Poison Arrow", e.Skill);
        Assert.Equal(300, e.Amount);
    }

    [Fact]
    public void ParsesDamageTaken()
    {
        var e = LogParser.Parse("2026.10.28 17:02:12 : Protectorate Hunter inflicted 640 damage on you.");

        Assert.NotNull(e);
        Assert.Equal(LogParser.You, e.Target);
    }

    [Theory]
    [InlineData("2026.10.28 17:03:24 : You have gained 825,736 XP from Protectorate Searcher (Energy of Repose 235,924).")]
    [InlineData("2026.10.28 17:02:10 : Critical Strike!Protectorate Hunter's attack speed has decreased because Grop used Dazing Severe Blow.")]
    [InlineData("2026.10.28 17:03:31 : You have earned 520 Kinah.")]
    [InlineData("")]
    public void IgnoresNonDamageLines(string line) => Assert.Null(LogParser.Parse(line));
}
