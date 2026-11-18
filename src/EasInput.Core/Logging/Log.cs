namespace EasInput.Core.Logging;

public enum LogLevel
{
    Error = 0,
    Warn = 1,
    Info = 2,
    Debug = 3,
}

/// <summary>A deliberately tiny file logger: one rotating file, never throws.</summary>
public static class Log
{
    private const long MaxBytes = 1_048_576;

    private static readonly object Gate = new();
    private static string? _path;
    private static LogLevel _minimum = LogLevel.Warn;

    public static void Init(string path, LogLevel minimum)
    {
        lock (Gate)
        {
            _path = path;
            _minimum = minimum;
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch
            {
                _path = null;
            }
        }
    }

    public static void Error(string message) => Write(LogLevel.Error, message);

    public static void Warn(string message) => Write(LogLevel.Warn, message);

    public static void Info(string message) => Write(LogLevel.Info, message);

    public static void Debug(string message) => Write(LogLevel.Debug, message);

    private static void Write(LogLevel level, string message)
    {
        if (level > _minimum)
        {
            return;
        }

        lock (Gate)
        {
            if (_path is null)
            {
                return;
            }

            try
            {
                Rotate();
                File.AppendAllText(
                    _path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never take the app down.
            }
        }
    }

    private static void Rotate()
    {
        var file = new FileInfo(_path!);
        if (!file.Exists || file.Length <= MaxBytes)
        {
            return;
        }

        var backup = _path + ".1";
        if (File.Exists(backup))
        {
            File.Delete(backup);
        }

        File.Move(_path!, backup);
    }
}
