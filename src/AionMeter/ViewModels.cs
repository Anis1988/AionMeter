using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using AionMeter.Core;

namespace AionMeter;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public static class Format
{
    public static string Number(double n)
    {
        if (n >= 1_000_000) return (n / 1_000_000).ToString("0.00", CultureInfo.InvariantCulture) + "M";
        if (n >= 10_000) return (n / 1_000).ToString("0.0", CultureInfo.InvariantCulture) + "k";
        return n.ToString("N0", CultureInfo.InvariantCulture);
    }

    public static string Percent(double fraction) =>
        (fraction * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    public static string Duration(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }
}

/// <summary>One player row, shared by the overlay and the details list.</summary>
public sealed class CombatantRow(string key) : Observable
{
    int rank;
    string name = "";
    AionClass cls;
    bool isYou, isMonster, isBoss;
    long damage, maxHit, healing, value;
    double dps, percent, barFraction, critRate, hps, perSecond;
    int hits;

    public string Key { get; } = key;

    public int Rank { get => rank; private set => Set(ref rank, value); }
    public string Name { get => name; private set => Set(ref name, value); }
    public bool IsYou { get => isYou; private set => Set(ref isYou, value); }
    public bool IsMonster { get => isMonster; private set => Set(ref isMonster, value); }
    public bool IsBoss { get => isBoss; private set => Set(ref isBoss, value); }
    public long Damage { get => damage; private set => Set(ref damage, value); }
    public double Dps { get => dps; private set => Set(ref dps, value); }
    public double Percent { get => percent; private set => Set(ref percent, value); }
    public double BarFraction { get => barFraction; private set => Set(ref barFraction, value); }
    public int Hits { get => hits; private set => Set(ref hits, value); }
    public double CritRate { get => critRate; private set => Set(ref critRate, value); }
    public long MaxHit { get => maxHit; private set => Set(ref maxHit, value); }
    public long Healing { get => healing; private set => Set(ref healing, value); }
    public double Hps { get => hps; private set => Set(ref hps, value); }

    /// <summary>Damage or healing, whichever the meter is showing.</summary>
    public long Value { get => value; private set => Set(ref this.value, value); }
    public double PerSecond { get => perSecond; private set => Set(ref perSecond, value); }

    public AionClass Class
    {
        get => cls;
        private set
        {
            if (cls == value) return;
            cls = value;
            Raise(nameof(Class));
            Raise(nameof(ClassName));
            Raise(nameof(Icon));
            Raise(nameof(ClassBrush));
        }
    }

    public string ClassName => IsBoss ? "Boss" : IsMonster ? "Monster" : cls == AionClass.Unknown ? "?" : cls.ToString();
    public ImageSource Icon => IsMonster ? ClassIcons.Monster(IsBoss) : ClassIcons.Get(cls);

    /// <summary>Gradient fill for the meter bar.</summary>
    public Brush ClassBrush => ClassIcons.BarBrush(cls, IsBoss, IsMonster);
    public Brush NameBrush => IsBoss ? ClassIcons.BossBrush : System.Windows.Media.Brushes.White;
    public Visibility YouVisibility => IsYou ? Visibility.Visible : Visibility.Collapsed;

    public string DamageText => Format.Number(Damage);
    public string DpsText => Format.Number(Dps);
    public string PercentText => Format.Percent(Percent);
    public string CritText => Format.Percent(CritRate);
    public string MaxHitText => Format.Number(MaxHit);
    public string HealingText => Format.Number(Healing);
    public string HpsText => Format.Number(Hps);
    public string ValueText => Format.Number(Value);
    public string PerSecondText => Format.Number(PerSecond) + "/s";
    public string PercentShort => $"{Percent * 100:0}%";
    public string Summary => $"{Format.Number(Value)}  ({Format.Number(PerSecond)}/s)  {Percent * 100:0}%";

    public void Update(Combatant c, int rank, long total, long top, double seconds, MeterMode mode)
    {
        Rank = rank;
        IsBoss = c.IsBoss;
        Name = c.IsBoss ? "BOSS " + c.DisplayName : c.DisplayName;
        IsYou = c.IsYou;
        IsMonster = c.IsMonster;
        Class = c.Class;
        Damage = c.Damage;
        Dps = seconds > 0 ? c.Damage / seconds : 0;
        Healing = c.Healing;
        Hps = seconds > 0 ? c.Healing / seconds : 0;
        Value = c.Total(mode);
        PerSecond = seconds > 0 ? Value / seconds : 0;
        Percent = total > 0 ? (double)Value / total : 0;
        BarFraction = top > 0 ? (double)Value / top : 0;
        Hits = c.Hits;
        CritRate = c.CritRate;
        MaxHit = c.MaxHit;
        foreach (var p in new[] { nameof(DamageText), nameof(DpsText), nameof(PercentText), nameof(CritText),
                     nameof(MaxHitText), nameof(HealingText), nameof(HpsText), nameof(ValueText), nameof(Summary),
                     nameof(YouVisibility), nameof(ClassName), nameof(Icon), nameof(ClassBrush), nameof(NameBrush),
                     nameof(PerSecondText), nameof(PercentShort) })
            Raise(p);
    }

    /// <summary>Updates <paramref name="rows"/> in place so selection and scrolling survive refreshes.</summary>
    public static void Sync(ObservableCollection<CombatantRow> rows, IReadOnlyList<Combatant> ranked, Encounter enc,
        MeterMode mode)
    {
        var total = ranked.Sum(c => c.Total(mode));
        var top = ranked.Count > 0 ? ranked.Max(c => c.Total(mode)) : 0;
        for (var i = 0; i < ranked.Count; i++)
        {
            var c = ranked[i];
            var index = -1;
            for (var j = i; j < rows.Count; j++)
                if (rows[j].Key == c.Name) { index = j; break; }

            if (index < 0)
                rows.Insert(i, new CombatantRow(c.Name));
            else if (index != i)
                rows.Move(index, i);
            rows[i].Update(c, i + 1, total, top, enc.Seconds, mode);
        }
        while (rows.Count > ranked.Count)
            rows.RemoveAt(rows.Count - 1);
    }
}

public sealed class SkillRow
{
    public required string Name { get; init; }
    public long Damage { get; init; }
    public double Percent { get; init; }
    public int Hits { get; init; }
    public double CritRate { get; init; }
    public long MaxHit { get; init; }
    public double Average => Hits == 0 ? 0 : (double)Damage / Hits;

    public string DamageText => Format.Number(Damage);
    public string PercentText => Format.Percent(Percent);
    public string CritText => Format.Percent(CritRate);
    public string MaxHitText => Format.Number(MaxHit);
    public string AverageText => Format.Number(Average);

    /// <summary>Damage skills, or heal skills in healing mode. <see cref="Damage"/> holds the amount either way.</summary>
    public static List<SkillRow> From(Combatant c, MeterMode mode) =>
        c.SkillsFor(mode).Values
            .OrderByDescending(s => s.Damage)
            .Select(s => new SkillRow
            {
                Name = s.Name,
                Damage = s.Damage,
                Percent = c.Total(mode) > 0 ? (double)s.Damage / c.Total(mode) : 0,
                Hits = s.Hits,
                CritRate = s.Hits > 0 ? (double)s.Crits / s.Hits : 0,
                MaxHit = s.MaxHit,
            })
            .ToList();
}
