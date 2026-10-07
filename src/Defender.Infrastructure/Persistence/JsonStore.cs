using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defender.Infrastructure.Persistence;

public interface IVersioned
{
    int SchemaVersion { get; set; }
}

public enum LoadStatus { Loaded, Missing, Corrupt, IncompatibleVersion, Migrated }

public readonly record struct LoadResult<T>(T Value, LoadStatus Status, string? Message);

/// <summary>
/// Versioned JSON files. Missing → defaults. Corrupt or newer-than-supported → defaults, and the bad file
/// is preserved as *.bad-&lt;timestamp&gt; so nothing is silently destroyed. Older versions are passed to a migrator.
/// </summary>
public sealed class JsonStore<T> where T : class, IVersioned
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Func<T> _defaults;
    private readonly int _currentVersion;
    private readonly Func<T, T?>? _migrate;

    public JsonStore(string path, int currentVersion, Func<T> defaults, Func<T, T?>? migrate = null)
    {
        FilePath = path;
        _currentVersion = currentVersion;
        _defaults = defaults;
        _migrate = migrate;
    }

    public string FilePath { get; }

    public LoadResult<T> Load()
    {
        if (!File.Exists(FilePath)) return new(_defaults(), LoadStatus.Missing, null);
        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(File.ReadAllText(FilePath), Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            Quarantine();
            return new(_defaults(), LoadStatus.Corrupt, $"{Path.GetFileName(FilePath)} could not be read ({ex.GetType().Name}); defaults restored.");
        }
        if (value is null)
        {
            Quarantine();
            return new(_defaults(), LoadStatus.Corrupt, $"{Path.GetFileName(FilePath)} was empty; defaults restored.");
        }
        if (value.SchemaVersion == _currentVersion) return new(value, LoadStatus.Loaded, null);
        if (value.SchemaVersion < _currentVersion && _migrate?.Invoke(value) is { } migrated)
        {
            migrated.SchemaVersion = _currentVersion;
            return new(migrated, LoadStatus.Migrated, $"{Path.GetFileName(FilePath)} migrated from v{value.SchemaVersion}.");
        }
        Quarantine();
        return new(_defaults(), LoadStatus.IncompatibleVersion,
            $"{Path.GetFileName(FilePath)} has schema v{value.SchemaVersion} (supported v{_currentVersion}); defaults restored.");
    }

    public void Save(T value)
    {
        value.SchemaVersion = _currentVersion;
        AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(value, Options));
    }

    public void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }

    private void Quarantine()
    {
        try { File.Move(FilePath, $"{FilePath}.bad-{DateTime.UtcNow:yyyyMMddHHmmss}", overwrite: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
