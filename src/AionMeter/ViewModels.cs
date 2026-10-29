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
    bool isYou, isMonster;
    long damage, maxHit;
    double dps, percent, barFraction, critRate;
    int hits;

    public string Key { get; } = key;

    public int Rank { get => rank; private set => Set(ref rank, value); }
    public string Name { get => name; private set => Set(ref name, value); }
    public bool IsYou { get => isYou; private set => Set(ref isYou, value); }
    public bool IsMonster { get => isMonster; private set => Set(ref isMonster, value); }
    public long Damage { get => damage; private set => Set(ref damage, value); }
    public double Dps { get => dps; private set => Set(ref dps, value); }
    public double Percent { get => percent; private set => Set(ref percent, value); }
    public double BarFraction { get => barFraction; private set => Set(ref barFraction, value); }
    public int Hits { get => hits; private set => Set(ref hits, value); }
    public double CritRate { get => critRate; private set => Set(ref critRate, value); }
    public long MaxHit { get => maxHit; private set => Set(ref maxHit, value); }

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

    public string ClassName => IsMonster ? "Monster" : cls == AionClass.Unknown ? "?" : cls.ToString();
    public ImageSource Icon => ClassIcons.Get(cls);
    public Brush ClassBrush => ClassIcons.Brush(cls);
    public Visibility YouVisibility => IsYou ? Visibility.Visible : Visibility.Collapsed;

    public string DamageText => Format.Number(Damage);
    public string DpsText => Format.Number(Dps);
    public string PercentText => Format.Percent(Percent);
    public string CritText => Format.Percent(CritRate);
    public string MaxHitText => Format.Number(MaxHit);
    public string Summary => $"{Format.Number(Damage)}  ({Format.Number(Dps)}/s)  {Percent * 100:0}%";

    public void Update(Combatant c, int rank, long total, long top, double seconds)
    {
        Rank = rank;
        Name = c.DisplayName;
        IsYou = c.IsYou;
        IsMonster = c.IsMonster;
        Class = c.Class;
        Damage = c.Damage;
        Dps = seconds > 0 ? c.Damage / seconds : 0;
        Percent = total > 0 ? (double)c.Damage / total : 0;
        BarFraction = top > 0 ? (double)c.Damage / top : 0;
        Hits = c.Hits;
        CritRate = c.CritRate;
        MaxHit = c.MaxHit;
        foreach (var p in new[] { nameof(DamageText), nameof(DpsText), nameof(PercentText), nameof(CritText),
                     nameof(MaxHitText), nameof(Summary), nameof(YouVisibility), nameof(ClassName) })
            Raise(p);
    }

    /// <summary>Updates <paramref name="rows"/> in place so selection and scrolling survive refreshes.</summary>
    public static void Sync(ObservableCollection<CombatantRow> rows, IReadOnlyList<Combatant> ranked, Encounter enc)
    {
        var total = ranked.Sum(c => c.Damage);
        var top = ranked.Count > 0 ? ranked.Max(c => c.Damage) : 0;
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
            rows[i].Update(c, i + 1, total, top, enc.Seconds);
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

    public static List<SkillRow> From(Combatant c) =>
        c.Skills.Values
            .OrderByDescending(s => s.Damage)
            .Select(s => new SkillRow
            {
                Name = s.Name,
                Damage = s.Damage,
                Percent = c.Damage > 0 ? (double)s.Damage / c.Damage : 0,
                Hits = s.Hits,
                CritRate = s.Hits > 0 ? (double)s.Crits / s.Hits : 0,
                MaxHit = s.MaxHit,
            })
            .ToList();
}
