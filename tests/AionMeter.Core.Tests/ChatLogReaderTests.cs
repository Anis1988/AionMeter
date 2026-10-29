using AionMeter.Core;

namespace AionMeter.Core.Tests;

public sealed class ChatLogReaderTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("aionmeter").FullName;
    string LogPath => Path.Combine(dir, "Chat.log");

    public void Dispose() => Directory.Delete(dir, recursive: true);

    [Fact]
    public void SkipsExistingLinesAndReadsNewOnes()
    {
        File.WriteAllText(LogPath, "old line\r\n");
        var reader = new ChatLogReader(LogPath);

        Assert.Empty(reader.ReadNewLines());
        File.AppendAllText(LogPath, "new line\r\npartial");
        Assert.Equal(["new line"], reader.ReadNewLines());
        File.AppendAllText(LogPath, " end\r\n");
        Assert.Equal(["partial end"], reader.ReadNewLines());
    }

    [Fact]
    public void ReadsFromStartWhenAsked()
    {
        File.WriteAllText(LogPath, "a\nb\n");
        Assert.Equal(["a", "b"], new ChatLogReader(LogPath, fromStart: true).ReadNewLines());
    }

    [Fact]
    public void StartsOverWhenFileIsCleared()
    {
        File.WriteAllText(LogPath, "some old text that is long\n");
        var reader = new ChatLogReader(LogPath);
        reader.ReadNewLines();

        File.WriteAllText(LogPath, "fresh\n");
        Assert.Equal(["fresh"], reader.ReadNewLines());
    }

    [Fact]
    public void ReadsFileCreatedAfterStart()
    {
        var reader = new ChatLogReader(LogPath);
        Assert.Empty(reader.ReadNewLines());

        File.WriteAllText(LogPath, "first\n");
        Assert.Equal(["first"], reader.ReadNewLines());
    }
}
