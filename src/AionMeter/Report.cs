using System.Runtime.InteropServices;
using System.Windows;
using AionMeter.Core;

namespace AionMeter;

/// <summary>Builds the plain-text damage list that the Copy buttons put on the clipboard.</summary>
public static class Report
{
    /// <param name="count">How many top players to include; 0 or less means everyone.</param>
    public static string Build(IEnumerable<CombatantRow> rows, int count = 0)
    {
        // One line, e.g. "1-Batte 456.7k  2-Grop 310.2k  3-Lumi 98.4k"
        var list = rows.OrderBy(r => r.Rank);
        return string.Join("  ", (count > 0 ? list.Take(count) : list)
            .Select(r => $"{r.Rank}-{r.Name} {r.DamageText}"));
    }

    /// <summary>Copies the list; returns false if there was nothing to copy or the clipboard was busy.</summary>
    public static bool Copy(Encounter enc, IEnumerable<CombatantRow> rows, int count = 0)
    {
        if (enc.IsEmpty)
            return false;
        var text = Build(rows, count);
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
