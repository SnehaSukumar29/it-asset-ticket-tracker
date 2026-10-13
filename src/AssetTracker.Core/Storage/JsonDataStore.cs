using System.Text.Json;
using System.Text.Json.Serialization;
using AssetTracker.Core.Models;

namespace AssetTracker.Core.Storage;

/// <summary>
/// Saves all data to a single JSON file. Writes go to a temporary file first and
/// are then swapped in, so a crash mid-save cannot leave a half-written data file.
/// </summary>
public sealed class JsonDataStore(string path)
{
    private const long MaxFileBytes = 50 * 1024 * 1024; // refuse to load suspiciously large files

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Path { get; } = System.IO.Path.GetFullPath(path);

    public static string DefaultPath() =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
            "AssetTracker", "tracker-data.json");

    public TrackerData Load()
    {
        if (!File.Exists(Path)) return new TrackerData();
        if (new FileInfo(Path).Length > MaxFileBytes)
            throw new InvalidDataException($"Data file {Path} is larger than {MaxFileBytes / 1024 / 1024} MB; refusing to load.");
        try
        {
            using var stream = File.OpenRead(Path);
            return JsonSerializer.Deserialize<TrackerData>(stream, Options)
                ?? throw new InvalidDataException("Data file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Data file {Path} is corrupt or was edited incorrectly: {ex.Message}", ex);
        }
    }

    public void Save(TrackerData data)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";
        using (var stream = File.Create(temp))
        {
            JsonSerializer.Serialize(stream, data, Options);
        }
        File.Move(temp, Path, overwrite: true);
    }
}
