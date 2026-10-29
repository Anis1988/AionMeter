using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using AionMeter.Core;

namespace AionMeter;

/// <summary>Builds the plain-text damage list that the Copy buttons put on the clipboard.</summary>
public static class Report
{
    public static string Build(Encounter enc, IEnumerable<CombatantRow> rows)
    {
        var list = rows.OrderBy(r => r.Rank).ToList();
        var total = list.Sum(r => r.Damage);
        var secs = enc.Seconds;

        var sb = new StringBuilder();
        sb.Append($"Aion Meter - {Format.Duration(secs)} - Total {Format.Number(total)}");
        sb.AppendLine($" - DPS {Format.Number(secs > 0 ? total / secs : 0)}");
        foreach (var r in list)
        {
            var cls = r.IsMonster || r.Class == AionClass.Unknown ? "" : $" [{r.Class}]";
            sb.AppendLine($"{r.Rank}. {r.Name}{cls} {r.DamageText} ({r.DpsText}/s) {r.PercentText}");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Copies the list; returns false if there was nothing to copy or the clipboard was busy.</summary>
    public static bool Copy(Encounter enc, IEnumerable<CombatantRow> rows)
    {
        if (enc.IsEmpty)
            return false;
        var text = Build(enc, rows);
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
