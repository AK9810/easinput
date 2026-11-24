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
    private const string MutexName = @"Local\easinput.singleton";

    /// <summary>Marks the instance a restart launched, so it waits for the handoff instead of quitting.</summary>
    private const string RestartArgument = "--restarted";

    private const int RestartHandoffMs = 3_000;

    private Mutex? _mutex;
    private bool _ownsMutex;
    private bool _restarting;
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

        var restarted = e.Args.Any(a => a.Equals(RestartArgument, StringComparison.OrdinalIgnoreCase));
        _mutex = AcquireMutex(restarted, out _ownsMutex);
        if (_mutex is null)
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
            OpenConfig,
            Restart,
            Shutdown);

        Log.Info($"Started. Config: {_configPath}");

        if (restarted)
        {
            _tray.Notify("已重启", "输入模式与 CapsLock 状态已重新读取。");
            Log.Info("Restarted by the tray menu.");
        }
        else if (!_config.FirstRunShown)
        {
            _tray.Notify(
                "easinput 已启动",
                "提示会出现在插入符下方。右键托盘图标可打开配置、开机自启或退出。");
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
    /// The tray's 重启: ends this process and starts a fresh one. Re-seeding the state in place
    /// would be a hand-maintained list of everything the controller owns; a missed entry makes the
    /// button lie to the user, while a new process is complete by construction. See ADR-0006.
    /// </summary>
    private void Restart()
    {
        if (_restarting)
        {
            return;
        }

        _restarting = true;

        // The drift is only observable here, right before it is wiped: what easinput remembers
        // beside what the system actually reports.
        Log.Warn($"Restart: {_controller?.DescribeState() ?? "controller unavailable"}");

        var released = ReleaseMutex();
        try
        {
            StartRestartedInstance();
        }
        catch (Exception ex)
        {
            Log.Error($"Restart failed, staying up: {ex.GetType().Name}: {ex.Message}");
            ReclaimMutex(released);
            _tray?.Notify("重启失败", "旧的 easinput 仍在运行，可以再试一次或直接退出。");
            _restarting = false;
            return;
        }

        // Report liveness at warning level: a false return proves Windows had already dropped the
        // hook after a timeout, which is one of the two known sources of drift.
        _keyboard?.Unhook(reportAtWarningLevel: true);
        _foreground?.Unhook(reportAtWarningLevel: true);

        Log.Info("Restart handed off; exiting.");
        Shutdown();
    }

    /// <summary>
    /// Acquires the single-instance mutex. A restarted instance can arrive before the old process
    /// has finished exiting, so it waits for the handoff instead of showing the "already running"
    /// balloon and quitting — which would leave the user with no easinput at all.
    /// </summary>
    private static Mutex? AcquireMutex(bool waitForHandoff, out bool owned)
    {
        owned = false;
        var deadline = Environment.TickCount64 + (waitForHandoff ? RestartHandoffMs : 0);
        while (true)
        {
            var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (createdNew)
            {
                owned = true;
                return mutex;
            }

            mutex.Dispose();
            if (Environment.TickCount64 >= deadline)
            {
                return null;
            }

            Thread.Sleep(50);
        }
    }

    /// <summary>
    /// Frees the single-instance mutex before the new process starts, so the handoff cannot be
    /// mistaken for a second launch. Returns false when there was nothing to release.
    /// </summary>
    private bool ReleaseMutex()
    {
        if (_mutex is null)
        {
            return false;
        }

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not owned on this thread; dropping the handle is enough.
            }
        }

        _mutex.Dispose();
        _mutex = null;
        _ownsMutex = false;
        return true;
    }

    private void ReclaimMutex(bool released)
    {
        if (!released)
        {
            return;
        }

        try
        {
            var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (createdNew)
            {
                _mutex = mutex;
                _ownsMutex = true;
                return;
            }

            // Something else claimed the singleton in the gap; OnExit then has nothing to release.
            mutex.Dispose();
            Log.Warn("Another instance claimed the mutex after the failed restart.");
        }
        catch (Exception ex)
        {
            Log.Error($"Mutex reclaim failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts this executable again, carrying the original arguments so switches and modes survive.
    /// Throws when the child could not be started; the caller decides what to do about it.
    /// </summary>
    private static void StartRestartedInstance()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            throw new InvalidOperationException("Environment.ProcessPath is empty.");
        }

        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
        var commandLine = Environment.GetCommandLineArgs();

        // `dotnet EasInput.App.dll` and similar hosts need the managed entry point passed back.
        if (string.Equals(
                System.IO.Path.GetFileNameWithoutExtension(executable),
                "dotnet",
                StringComparison.OrdinalIgnoreCase) &&
            commandLine.Length > 1)
        {
            startInfo.ArgumentList.Add(commandLine[0]);
        }

        foreach (var argument in commandLine.Skip(1))
        {
            if (!argument.Equals(RestartArgument, StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        startInfo.ArgumentList.Add(RestartArgument);

        using var child = Process.Start(startInfo) ??
                          throw new InvalidOperationException("Process.Start returned null.");
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
            "它已经在后台工作，右键托盘图标可打开配置或退出。",
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
