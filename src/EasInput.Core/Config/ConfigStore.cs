using System.Text.Encodings.Web;
using System.Text.Json;

namespace EasInput.Core;

public static class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Portable first: a config.json next to the executable wins; otherwise the file lives in
    /// %APPDATA%\easinput.
    /// </summary>
    public static string ResolvePath(string? baseDirectory = null)
    {
        var directory = baseDirectory ?? AppContext.BaseDirectory;
        var portable = Path.Combine(directory, "config.json");
        if (File.Exists(portable))
        {
            return portable;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "easinput",
            "config.json");
    }

    public static AppConfig Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                var fresh = new AppConfig().Normalize();
                Save(path, fresh);
                return fresh;
            }

            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Options);
            if (config is null)
            {
                Logging.Log.Warn($"Config at {path} deserialized to null; using defaults.");
                return new AppConfig().Normalize();
            }

            return config.Normalize();
        }
        catch (Exception ex)
        {
            Logging.Log.Warn($"Failed to load config from {path}: {ex.Message}. Using defaults.");
            return new AppConfig().Normalize();
        }
    }

    public static void Save(string path, AppConfig config)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
        }
        catch (Exception ex)
        {
            Logging.Log.Warn($"Failed to save config to {path}: {ex.Message}.");
        }
    }
}
