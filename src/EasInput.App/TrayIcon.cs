using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EasInput.Core;
using Color = System.Drawing.Color;

namespace EasInput.App;

/// <summary>Tray icon is the only way to pause, configure, or quit a windowless background app.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly ToolStripMenuItem _enabled;
    private readonly ToolStripMenuItem _autostart;
    private IntPtr _iconHandle;

    public TrayIcon(
        AppConfig config,
        Action<bool> enabledChanged,
        Action<bool> autostartChanged,
        Action openConfig,
        Action exit)
    {
        _enabled = new ToolStripMenuItem("启用提示")
        {
            Checked = config.Enabled,
            CheckOnClick = true,
        };
        _enabled.Click += (_, _) => enabledChanged(_enabled.Checked);

        _autostart = new ToolStripMenuItem("开机自启")
        {
            Checked = Autostart.IsEnabled(),
            CheckOnClick = true,
        };
        _autostart.Click += (_, _) => autostartChanged(_autostart.Checked);

        var menu = new ContextMenuStrip();

        menu.Items.Add(_enabled);
        menu.Items.Add(_autostart);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("打开配置文件", null, (_, _) => openConfig()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => exit()));

        var icon = CreateIcon(config, out _iconHandle);
        _notify = new NotifyIcon
        {
            Icon = icon,
            Text = "easinput — 输入模式提示",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notify.DoubleClick += (_, _) => openConfig();
    }

    /// <summary>Shows a balloon tip from the tray icon.</summary>
    public void Notify(string title, string text)
    {
        _notify.ShowBalloonTip(5000, title, text, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();

        DestroyIconHandle(_iconHandle);
        _iconHandle = IntPtr.Zero;
    }

    /// <summary>
    /// Draws the brand icon. The caller owns <paramref name="iconHandle"/> and must release it
    /// with <see cref="DestroyIconHandle"/> once the <see cref="Icon"/> is no longer in use.
    /// </summary>
    public static Icon CreateIcon(AppConfig config, out IntPtr iconHandle)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            using var path = RoundedRect(new RectangleF(1, 1, 30, 30), 9);
            using var fill = new SolidBrush(ParseColor(config.PillColor));
            graphics.FillPath(fill, path);

            using var font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var text = new SolidBrush(Color.White);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            graphics.DrawString(config.ShortCodes.Chinese, font, text, new RectangleF(0, 0, 32, 32), format);
        }

        iconHandle = bitmap.GetHicon();
        return Icon.FromHandle(iconHandle);
    }

    /// <summary>Destroys a handle returned by <see cref="CreateIcon"/>.</summary>
    public static void DestroyIconHandle(IntPtr iconHandle)
    {
        if (iconHandle != IntPtr.Zero)
        {
            DestroyIcon(iconHandle);
        }
    }

    private static Color ParseColor(string text)
    {
        var fallback = Color.FromArgb(255, 0, 122, 255);
        var value = (text ?? string.Empty).Trim();
        if (value.Length != 7 || value[0] != '#')
        {
            return fallback;
        }

        if (!int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return fallback;
        }

        return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }

    private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
