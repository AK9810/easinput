using EasInput.Core;

namespace EasInput.Core.Tests;

public class DpiScaleTests
{
    [Theory]
    [InlineData(96u, 1.0)]
    [InlineData(120u, 1.25)]
    [InlineData(144u, 1.5)]
    [InlineData(192u, 2.0)]
    [InlineData(0u, 1.0)]
    public void FromDpi_MapsToScale(uint dpi, double expected)
    {
        Assert.Equal(expected, DpiScale.FromDpi(dpi));
    }

    [Theory]
    [InlineData(1.25, 29)]
    [InlineData(1.5, 35)]
    [InlineData(2.0, 46)]
    public void Ceil_RoundsUpToPhysicalPixels(double scale, int expected)
    {
        Assert.Equal(expected, DpiScale.Ceil(23, scale));
    }

    [Theory]
    [InlineData(1.25, 4)]
    [InlineData(1.5, 5)]
    [InlineData(2.0, 6)]
    public void Round_NearestPhysicalPixels(double scale, int expected)
    {
        Assert.Equal(expected, DpiScale.Round(3, scale));
    }
}
