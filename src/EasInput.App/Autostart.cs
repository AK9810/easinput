using System.IO;
using System.Reflection;
using EasInput.Core.Logging;
using Microsoft.Win32;

namespace EasInput.App;

public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "easinput";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to read autostart: {ex.Message}");
            return false;
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                var executable = Environment.ProcessPath;
                if (string.IsNullOrEmpty(executable))
                {
                    return;
                }

                var command = $"\"{executable}\"";
                if (string.Equals(
                        Path.GetFileNameWithoutExtension(executable),
                        "dotnet",
                        StringComparison.OrdinalIgnoreCase))
                {
                    // Dev runs are `dotnet easinput.dll`; the Run key needs the DLL as an argument.
                    var assembly = Assembly.GetEntryAssembly()?.Location;
                    if (string.IsNullOrEmpty(assembly))
                    {
                        return;
                    }

                    command += $" \"{assembly}\"";
                }

                key.SetValue(ValueName, command);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to update autostart: {ex.Message}");
        }
    }
}
