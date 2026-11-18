using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using EasInput.Core;
using EasInput.Core.Interop;
using EasInput.Core.Logging;
using Application = System.Windows.Application;

namespace EasInput.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private BubbleWindow? _bubble;
    private BubbleController? _controller;
    private KeyboardHook? _keyboard;
    private ForegroundWatcher? _foreground;
    private TrayIcon? _tray;
    private AppConfig _config = new();
    private string _configPath = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _configPath = ConfigStore.ResolvePath();
        _config = ConfigStore.Load(_configPath);
        Log.Init(LogPath(), ParseLevel(_config.LogLevel));

        if (e.Args.Any(a => a.StartsWith("--demo", StringComparison.OrdinalIgnoreCase)))
        {
            RunDemo(e.Args);
            return;
        }

        if (e.Args.Any(a => a.Equals("--probe", StringComparison.OrdinalIgnoreCase)))
        {
            ProbeMode.Run();
            Shutdown();
            return;
        }

        if (e.Args.Any(a => a.Equals("--calibrate", StringComparison.OrdinalIgnoreCase)))
        {
            CalibrateMode.Run();
            Shutdown();
            return;
        }

        _mutex = new Mutex(initiallyOwned: true, @"Local\easinput.singleton", out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            Log.Info("Another instance is already running; showing a hint and exiting.");
            NotifyAlreadyRunning();
            return;
        }

        // The JSON setting is authoritative at startup; the tray then reflects the registry state.
        Autostart.Set(_config.Autostart);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error($"Unhandled exception: {args.Exception}");
            args.Handled = true;
        };

        _bubble = new BubbleWindow();
        _bubble.Warm();

        _controller = new BubbleController(_config, _bubble, Dispatcher);
        _controller.Start();

        _keyboard = new KeyboardHook(Dispatcher, _controller);
        if (!_keyboard.Install())
        {
            Log.Warn("Low-level keyboard hook failed to install.");
        }

        _foreground = new ForegroundWatcher(Dispatcher, _controller);
        if (!_foreground.Install())
        {
            Log.Warn("Foreground WinEvent hook failed to install.");
        }

        _tray = new TrayIcon(
            _config,
            enabled =>
            {
                _config.Enabled = enabled;
                ConfigStore.Save(_configPath, _config);
                _controller?.OnEnabledChanged();
            },
            autostart =>
            {
                _config.Autostart = autostart;
                Autostart.Set(autostart);
                ConfigStore.Save(_configPath, _config);
            },
            _controller.Preview,
            OpenConfig,
            Shutdown);

        Log.Info($"Started. Config: {_configPath}");

        if (e.Args.Any(a => a.Equals("--preview", StringComparison.OrdinalIgnoreCase)))
        {
            _controller.Preview(NativeMethods.GetForegroundWindow());
            var quit = new DispatcherTimer(
                TimeSpan.FromSeconds(4),
                DispatcherPriority.Normal,
                (_, _) => Shutdown(),
                Dispatcher);
            quit.Start();
        }

        if (!_config.FirstRunShown)
        {
            _tray.Notify(
                "easinput 已启动",
                "提示会出现在光标下方。右键托盘图标可预览效果、开机自启或退出。");
            _config.FirstRunShown = true;
            ConfigStore.Save(_configPath, _config);
            Log.Info("First-run hint shown.");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _keyboard?.Dispose();
        _foreground?.Dispose();
        _controller?.Dispose();

        if (_mutex is not null)
        {
            if (_ownsMutex)
            {
                _mutex.ReleaseMutex();
            }

            _mutex.Dispose();
        }

        Log.Info("Exited.");
        base.OnExit(e);
    }

    /// <summary>
    /// A second launch has no window to show, so a temporary tray balloon is the only feedback.
    /// The timer keeps this process alive long enough for the balloon to appear.
    /// </summary>
    private void NotifyAlreadyRunning()
    {
        var icon = TrayIcon.CreateIcon(_config, out var iconHandle);
        var notify = new NotifyIcon
        {
            Icon = icon,
            Text = "easinput",
            Visible = true,
        };

        notify.ShowBalloonTip(
            5000,
            "easinput 已在运行",
            "它已经在后台工作，右键托盘图标可预览或退出。",
            ToolTipIcon.Info);

        var timer = new DispatcherTimer(
            TimeSpan.FromSeconds(3),
            DispatcherPriority.Normal,
            (_, _) =>
            {
                notify.Visible = false;
                notify.Dispose();
                TrayIcon.DestroyIconHandle(iconHandle);
                Shutdown();
            },
            Dispatcher);
        timer.Start();
    }

    private void RunDemo(string[] args)
    {
        var code = "拼";
        var demo = args.FirstOrDefault(a => a.StartsWith("--demo=", StringComparison.OrdinalIgnoreCase));
        if (demo is not null)
        {
            code = demo["--demo=".Length..];
        }

        PixelRect anchor;
        var at = args.FirstOrDefault(a => a.StartsWith("--demo-at=", StringComparison.OrdinalIgnoreCase));
        var rectArg = args.FirstOrDefault(a => a.StartsWith("--demo-rect=", StringComparison.OrdinalIgnoreCase));
        if (rectArg is not null && TryParseRect(rectArg["--demo-rect=".Length..], out var rect))
        {
            anchor = rect;
        }
        else if (at is not null && TryParsePoint(at["--demo-at=".Length..], out var x, out var y))
        {
            anchor = PixelRect.FromLtrb(x, y, x + 1, y + 1);
        }
        else if (new Win32CaretLocator().TryLocate(NativeMethods.GetForegroundWindow(), out var caret))
        {
            anchor = caret;
        }
        else
        {
            var work = WorkArea.Get(0, 0);
            var centerX = work.Left + (work.Width / 2);
            var centerY = work.Top + (work.Height / 2);
            anchor = PixelRect.FromLtrb(centerX, centerY, centerX + 1, centerY + 1);
        }

        _bubble = new BubbleWindow();
        _config.HoldMs = 4_000; // keep it on screen long enough to inspect or screenshot
        var mode = code == _config.ShortCodes.Chinese
            ? InputMode.Chinese
            : code == _config.ShortCodes.LatinUpper
                ? InputMode.LatinUpper
                : InputMode.LatinLower;
        _bubble.ShowAt(anchor, mode, code, _config, WorkArea.Get(anchor.Left, anchor.Top));

        var timer = new DispatcherTimer(
            TimeSpan.FromSeconds(6),
            DispatcherPriority.Normal,
            (_, _) => Shutdown(),
            Dispatcher);
        timer.Start();
    }

    private static bool TryParsePoint(string text, out int x, out int y)
    {
        x = 0;
        y = 0;
        var parts = text.Split(',');
        return parts.Length == 2 &&
               int.TryParse(parts[0], out x) &&
               int.TryParse(parts[1], out y);
    }

    private static bool TryParseRect(string text, out PixelRect rect)
    {
        rect = default;
        var parts = text.Split(',');
        if (parts.Length != 4 ||
            !int.TryParse(parts[0], out var left) ||
            !int.TryParse(parts[1], out var top) ||
            !int.TryParse(parts[2], out var right) ||
            !int.TryParse(parts[3], out var bottom))
        {
            return false;
        }

        rect = PixelRect.FromLtrb(left, top, right, bottom);
        return true;
    }

    private static string LogPath() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "easinput",
        "easinput.log");

    private static LogLevel ParseLevel(string text) =>
        Enum.TryParse<LogLevel>(text, ignoreCase: true, out var level) ? level : LogLevel.Warn;

    private void OpenConfig()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_configPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to open config: {ex.Message}");
        }
    }
}
