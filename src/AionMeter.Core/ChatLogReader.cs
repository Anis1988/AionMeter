using System.Globalization;
using System.Text;

namespace AionMeter.Core;

/// <summary>
/// Follows Chat.log as the game appends to it. The file is reopened on every read
/// so it keeps working when the game or another tool clears or recreates it.
/// </summary>
public sealed class ChatLogReader
{
    readonly StringBuilder pending = new();
    Encoding? encoding;
    Decoder? decoder;
    long position;
    bool started;

    static ChatLogReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <param name="path">Full path to Chat.log.</param>
    /// <param name="fromStart">Also return lines already in the file.</param>
    public ChatLogReader(string path, bool fromStart = false)
    {
        Path = path;
        started = fromStart;
    }

    public string Path { get; }
    public bool Exists => File.Exists(Path);

    /// <summary>
    /// Empties the log so it doesn't grow forever. Best done before the game starts:
    /// if the game is writing it may pad the file with zero bytes, which the reader skips.
    /// </summary>
    /// <returns>The size in bytes that was cleared, or 0 if there was nothing to clear.</returns>
    public static long Clear(string path)
    {
        if (!File.Exists(path))
            return 0;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        var size = fs.Length;
        fs.SetLength(0);
        return size;
    }

    public IReadOnlyList<string> ReadNewLines()
    {
        var lines = new List<string>();
        if (!File.Exists(Path))
        {
            // Once it appears, everything in it is new.
            started = true;
            return lines;
        }

        using var fs = new FileStream(Path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (!started)
        {
            // Skip what was logged before the meter started.
            started = true;
            position = fs.Length;
        }

        if (encoding == null || fs.Length < position)
        {
            // First read, or the file was cleared: start over from the top.
            if (fs.Length < position)
                position = 0;
            encoding = DetectEncoding(fs);
            decoder = encoding.GetDecoder();
            pending.Clear();
            if (position == 0 && StartsWithPreamble(fs, encoding))
                position = encoding.Preamble.Length;
        }

        if (fs.Length == position)
            return lines;

        fs.Seek(position, SeekOrigin.Begin);
        var bytes = new byte[fs.Length - position];
        var read = fs.Read(bytes, 0, bytes.Length);
        position += read;

        var chars = new char[decoder!.GetCharCount(bytes, 0, read)];
        decoder.GetChars(bytes, 0, read, chars, 0);
        pending.Append(chars);

        // Zero bytes appear if the file was cleared while the game kept writing at its old offset.
        var text = pending.ToString().Replace("\0", "");
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
                continue;
            var line = text[start..i].TrimEnd('\r');
            if (line.Length > 0)
                lines.Add(line);
            start = i + 1;
        }
        pending.Clear().Append(text, start, text.Length - start);
        return lines;
    }

    static Encoding DetectEncoding(FileStream fs)
    {
        var head = new byte[3];
        fs.Seek(0, SeekOrigin.Begin);
        var n = fs.Read(head, 0, 3);
        if (n >= 2 && head[0] == 0xFF && head[1] == 0xFE)
            return new UnicodeEncoding(false, true);
        if (n >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
            return new UTF8Encoding(true);
        // The Aion client writes the Windows ANSI code page.
        var codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
        try
        {
            return Encoding.GetEncoding(codePage > 0 ? codePage : 1252);
        }
        catch (ArgumentException)
        {
            return Encoding.Latin1;
        }
    }

    static bool StartsWithPreamble(FileStream fs, Encoding enc)
    {
        var preamble = enc.Preamble;
        if (preamble.Length == 0 || fs.Length < preamble.Length)
            return false;
        var head = new byte[preamble.Length];
        fs.Seek(0, SeekOrigin.Begin);
        return fs.Read(head, 0, head.Length) == head.Length && head.AsSpan().SequenceEqual(preamble);
    }
}
