namespace EasInput.Core;

public sealed class AppConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>How long the bubble stays fully visible before fading out.</summary>
    public int HoldMs { get; set; } = 1000;

    public int FadeInMs { get; set; } = 120;

    public int FadeOutMs { get; set; } = 200;

    /// <summary>Distance between the caret and the bubble, in device-independent pixels.</summary>
    public int GapDip { get; set; } = 3;

    public int PillHeight { get; set; } = 23;

    public string PillColor { get; set; } = "#007AFF";

    public string FontFamily { get; set; } = "Segoe UI Variable Display";

    public double FontSize { get; set; } = 12;

    public ShortCodeMap ShortCodes { get; set; } = new();

    public bool Autostart { get; set; }

    public string LogLevel { get; set; } = "warn";

    public AppConfig Normalize()
    {
        HoldMs = Math.Clamp(HoldMs, 100, 10_000);
        FadeInMs = Math.Clamp(FadeInMs, 0, 2_000);
        FadeOutMs = Math.Clamp(FadeOutMs, 0, 2_000);
        GapDip = Math.Clamp(GapDip, 0, 32);
        PillHeight = Math.Clamp(PillHeight, 12, 64);
        FontSize = Math.Clamp(FontSize, 6, 48);

        PillColor = (PillColor ?? string.Empty).Trim();
        if (!IsHexColor(PillColor))
        {
            PillColor = "#007AFF";
        }

        ShortCodes ??= new ShortCodeMap();
        ShortCodes.Normalize();

        if (!Enum.TryParse<Logging.LogLevel>(LogLevel, ignoreCase: true, out _))
        {
            LogLevel = "warn";
        }

        return this;
    }

    private static bool IsHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.AsSpan().Trim();
        if (text.Length is not (7 or 9) || text[0] != '#')
        {
            return false;
        }

        foreach (var c in text[1..])
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class ShortCodeMap
{
    public string Chinese { get; set; } = "拼";

    public string LatinLower { get; set; } = "a";

    public string LatinUpper { get; set; } = "A";

    public string For(InputMode mode) => mode switch
    {
        InputMode.Chinese => Chinese,
        InputMode.LatinUpper => LatinUpper,
        _ => LatinLower,
    };

    public void Normalize()
    {
        Chinese = FirstGlyph(Chinese, "拼");
        LatinLower = FirstGlyph(LatinLower, "a");
        LatinUpper = FirstGlyph(LatinUpper, "A");
    }

    private static string FirstGlyph(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();
        return text.Length <= 2 ? text : text[..2];
    }
}
