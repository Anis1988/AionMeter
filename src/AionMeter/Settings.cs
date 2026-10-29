using System.Text.Json;
using System.Text.Json.Serialization;
using AionMeter.Core;

namespace AionMeter;

public sealed class Settings
{
    static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AionMeter");
    static readonly string FilePath = Path.Combine(Folder, "settings.json");

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    public string GamePath { get; set; } = @"C:\GamezAION 4.8\GamezAION 4.8";
    public string CharacterName { get; set; } = "";
    public AionClass YourClass { get; set; } = AionClass.Unknown;
    public bool ShowMonsters { get; set; }
    public bool MergeSummons { get; set; } = true;

    /// <summary>How many top players the Copy button copies; 0 copies everyone.</summary>
    public int CopyCount { get; set; } = 10;
    public double Opacity { get; set; } = 0.92;

    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 330;
    public double Height { get; set; } = 280;

    /// <summary>Classes set by hand, by character name.</summary>
    public Dictionary<string, AionClass> ClassOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Skill → class pairs learned while playing.</summary>
    public Dictionary<string, AionClass> LearnedSkills { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public string ChatLogPath => Path.Combine(GamePath, "Chat.log");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fall back to defaults if the file is unreadable.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not worth crashing the meter over.
        }
    }
}
