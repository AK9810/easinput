using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EasInput.Core;
using EasInput.Core.Interop;
using EasInput.Core.Logging;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using DpiScale = EasInput.Core.DpiScale;
using FontFamily = System.Windows.Media.FontFamily;
using Size = System.Windows.Size;

namespace EasInput.App;

/// <summary>
/// The overlay. Kept alive but hidden between shows; the window is never activated, never appears
/// in the taskbar or Alt+Tab, and passes every click through.
/// </summary>
public partial class BubbleWindow : Window
{
    /// <summary>Room around the pill so the drop shadow is not clipped by the window bounds.</summary>
    private const double ShadowPadding = 16;

    private AppConfig _config = new();
    private DispatcherTimer? _hold;
    private bool _warm;
    private bool _showing;
    private int _generation;

    public BubbleWindow()
    {
        InitializeComponent();
    }

    public bool IsShowing => _showing;

    /// <summary>Creates the handle and lays out once, so the first real show has no startup cost.</summary>
    public void Warm()
    {
        if (_warm)
        {
            return;
        }

        _warm = true;
        Left = -20_000;
        Top = -20_000;
        Opacity = 0;
        Show();
        Hide();
        ApplyExtendedStyles();
        Log.Debug($"Warm: hwnd=0x{new WindowInteropHelper(this).Handle.ToInt64():X} size={ActualWidth}x{ActualHeight}");
    }

    public void ShowAt(PixelRect anchor, InputMode mode, string code, AppConfig config, PixelRect workArea)
    {
        _config = config;
        ApplyStyle(config, mode);
        Code.Text = code;

        Warm();

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // The anchor is physical screen pixels, so use the DPI of the monitor under it. The
        // window's own DPI still belongs to the previous show's monitor until it is moved.
        var scale = DpiScale.ForPoint(anchor.Left, anchor.Top);

        Pill.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var pillWidth = DpiScale.Ceil(Pill.DesiredSize.Width, scale);
        var pillHeight = DpiScale.Ceil(Pill.DesiredSize.Height, scale);
        var gap = DpiScale.Round(config.GapDip, scale);
        var target = BubblePlacement.Compute(anchor, pillWidth, pillHeight, gap, workArea);
        var padding = DpiScale.Round(ShadowPadding, scale);

        if (!IsVisible)
        {
            Show();
        }

        Log.Debug(
            $"Show: code={code} anchor={anchor.Left},{anchor.Top},{anchor.Right},{anchor.Bottom} " +
            $"scale={scale} pill={pillWidth}x{pillHeight} target={target.Left},{target.Top} " +
            $"window={target.Left - padding},{target.Top - padding} visible={IsVisible}");

        var moved = NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            target.Left - padding,
            target.Top - padding,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        NativeMethods.GetWindowRect(hwnd, out var actual);
        Log.Debug(
            $"Placed: ok={moved} actual={actual.Left},{actual.Top},{actual.Right},{actual.Bottom} " +
            $"winVisible={NativeMethods.IsWindowVisible(hwnd)} opacity={Opacity}");

        if (!_showing)
        {
            _showing = true;
            _generation++;
            Opacity = 0;
            BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(Math.Max(1, config.FadeInMs)))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                });
        }

        RestartHold();
    }

    public void HideNow()
    {
        if (!_showing)
        {
            _hold?.Stop();
            return;
        }

        BeginFadeOut();
    }

    private void RestartHold()
    {
        _hold?.Stop();
        _hold = new DispatcherTimer(
            TimeSpan.FromMilliseconds(_config.HoldMs),
            DispatcherPriority.Normal,
            (_, _) => BeginFadeOut(),
            Dispatcher);
        _hold.Start();
    }

    private void BeginFadeOut()
    {
        _hold?.Stop();
        _hold = null;
        if (!_showing)
        {
            return;
        }

        _showing = false;
        Log.Debug($"Hide: code={Code.Text}");
        var generation = ++_generation;
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(Math.Max(1, _config.FadeOutMs)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        fade.Completed += (_, _) =>
        {
            if (generation != _generation)
            {
                return;
            }

            Hide();
            Opacity = 0;
        };
        BeginAnimation(OpacityProperty, fade);
    }

    private void ApplyExtendedStyles()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_NOACTIVATE
                 | NativeMethods.WS_EX_TOOLWINDOW
                 | NativeMethods.WS_EX_TRANSPARENT
                 | NativeMethods.WS_EX_LAYERED;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
    }

    private void ApplyStyle(AppConfig config, InputMode mode)
    {
        Pill.Height = config.PillHeight;
        Pill.CornerRadius = new CornerRadius(config.PillHeight / 2.0);

        var horizontal = config.PillHeight * 0.39;
        Pill.Padding = new Thickness(horizontal, 0, horizontal, 0);

        if (TryParseBrush(config.PillColor, out var background))
        {
            Pill.Background = background;
        }

        Code.FontFamily = new FontFamily(config.FontFamily);
        Code.FontSize = config.FontSize;

        // The CJK fallback is heavier than SF Pro's regular weight at this size, so use Normal
        // for Chinese and SemiBold for the Latin a/A, matching the reference's optical weight.
        Code.FontWeight = mode == InputMode.Chinese ? FontWeights.Normal : FontWeights.SemiBold;
    }

    private static bool TryParseBrush(string text, out Brush brush)
    {
        brush = Brushes.DodgerBlue;
        try
        {
            if (new BrushConverter().ConvertFromString(text) is Brush parsed)
            {
                brush = parsed;
                return true;
            }
        }
        catch
        {
            // Fall through to the default brush.
        }

        return false;
    }
}
