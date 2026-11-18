using EasInput.Core;

namespace EasInput.Core.Tests;

public class AppConfigTests
{
    [Fact]
    public void ClampsOutOfRangeValues()
    {
        var config = new AppConfig
        {
            HoldMs = 0,
            FadeInMs = -5,
            FadeOutMs = 99_999,
            GapDip = 999,
            PillHeight = 1,
            FontSize = 200,
        }.Normalize();

        Assert.Equal(100, config.HoldMs);
        Assert.Equal(0, config.FadeInMs);
        Assert.Equal(2_000, config.FadeOutMs);
        Assert.Equal(32, config.GapDip);
        Assert.Equal(12, config.PillHeight);
        Assert.Equal(48, config.FontSize);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void RejectsInvalidColors(string color)
    {
        var config = new AppConfig { PillColor = color }.Normalize();

        Assert.Equal("#007AFF", config.PillColor);
    }

    [Theory]
    [InlineData("#007AFF")]
    [InlineData("#80007AFF")]
    [InlineData(" #abcdef ")]
    public void KeepsValidColors(string color)
    {
        var config = new AppConfig { PillColor = color }.Normalize();

        Assert.Equal(color.Trim(), config.PillColor);
    }

    [Fact]
    public void FallsBackToDefaultShortCodes()
    {
        var config = new AppConfig
        {
            ShortCodes = new ShortCodeMap { Chinese = "", LatinLower = null!, LatinUpper = "  " },
        }.Normalize();

        Assert.Equal("拼", config.ShortCodes.Chinese);
        Assert.Equal("a", config.ShortCodes.LatinLower);
        Assert.Equal("A", config.ShortCodes.LatinUpper);
    }

    [Fact]
    public void UnknownLogLevelFallsBackToWarn()
    {
        var config = new AppConfig { LogLevel = "verbose" }.Normalize();

        Assert.Equal("warn", config.LogLevel);
    }

    [Theory]
    [InlineData(InputMode.Chinese, "拼")]
    [InlineData(InputMode.LatinLower, "a")]
    [InlineData(InputMode.LatinUpper, "A")]
    public void ShortCodeMapMapsEveryMode(InputMode mode, string expected)
    {
        Assert.Equal(expected, new ShortCodeMap().For(mode));
    }
}
