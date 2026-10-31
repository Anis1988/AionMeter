using System.Runtime.InteropServices;
using System.Windows;
using AionMeter.Core;

namespace AionMeter;

/// <summary>Builds the plain-text damage list that the Copy buttons put on the clipboard.</summary>
public static class Report
{
    /// <param name="count">How many top players to include; 0 or less means everyone.</param>
    /// <param name="target">The target picked on the meter, which goes in front instead.</param>
    public static string Build(IEnumerable<CombatantRow> rows, int count = 0, string? target = null)
    {
        // One line, e.g. "[Kromede] 1-Batte 456.7k  2-Grop 310.2k  3-Lumi 98.4k".
        // A shown boss (or else the top monster) goes in front; only players are ranked.
        var all = rows.OrderBy(r => r.Rank).ToList();
        var mob = target ?? (all.FirstOrDefault(r => r.IsBoss) ?? all.FirstOrDefault(r => r.IsMonster))?.Key;
        var players = all.Where(r => !r.IsMonster);
        var ranking = string.Join("  ", (count > 0 ? players.Take(count) : players)
            .Select((r, i) => $"{i + 1}-{r.Name} {r.ValueText}"));
        // Key is the plain log name, without the "BOSS " prefix shown in the meter.
        return mob == null ? ranking : $"[{mob}] {ranking}".TrimEnd();
    }

    /// <summary>Copies the list; returns false if there was nothing to copy or the clipboard was busy.</summary>
    public static bool Copy(Encounter enc, IEnumerable<CombatantRow> rows, int count = 0, string? target = null)
    {
        var text = Build(rows, count, target);
        if (enc.IsEmpty || text.Length == 0)
            return false;
        // Another app can briefly hold the clipboard open, so retry a few times.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(50);
            }
        }
        return false;
    }
}
