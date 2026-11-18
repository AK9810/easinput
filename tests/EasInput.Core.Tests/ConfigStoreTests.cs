using EasInput.Core;

namespace EasInput.Core.Tests;

public class ConfigStoreTests
{
    [Fact]
    public void RoundTripsThroughJson()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "config.json");
        var original = new AppConfig
        {
            Enabled = false,
            HoldMs = 1500,
            PillColor = "#112233",
            ShortCodes = new ShortCodeMap { Chinese = "微" },
            LogLevel = "info",
        };

        ConfigStore.Save(path, original);
        var loaded = ConfigStore.Load(path);

        Assert.False(loaded.Enabled);
        Assert.Equal(1500, loaded.HoldMs);
        Assert.Equal("#112233", loaded.PillColor);
        Assert.Equal("微", loaded.ShortCodes.Chinese);
        Assert.Equal("info", loaded.LogLevel);
    }

    [Fact]
    public void RoundTripsFirstRunShown()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "config.json");

        ConfigStore.Save(path, new AppConfig { FirstRunShown = true });

        Assert.True(ConfigStore.Load(path).FirstRunShown);
    }

    [Fact]
    public void OldConfigWithoutFirstRunShownLoadsAsFalse()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "config.json");
        File.WriteAllText(path, "{ \"enabled\": true, \"holdMs\": 1200 }");

        var loaded = ConfigStore.Load(path);

        Assert.False(loaded.FirstRunShown);
        Assert.Equal(1200, loaded.HoldMs);
    }

    [Fact]
    public void MissingFileIsCreatedWithDefaults()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "config.json");

        var loaded = ConfigStore.Load(path);

        Assert.True(File.Exists(path));
        Assert.True(loaded.Enabled);
        Assert.Equal("拼", loaded.ShortCodes.Chinese);
    }

    [Fact]
    public void CorruptFileFallsBackToDefaults()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "config.json");
        File.WriteAllText(path, "{ this is not json");

        var loaded = ConfigStore.Load(path);

        Assert.True(loaded.Enabled);
        Assert.Equal(1000, loaded.HoldMs);
    }

    [Fact]
    public void PortableConfigWinsOverAppData()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "config.json"), "{}");

        var resolved = ConfigStore.ResolvePath(temp.Path);

        Assert.Equal(Path.Combine(temp.Path, "config.json"), resolved);
    }

    [Fact]
    public void WithoutPortableConfigFallsBackToAppData()
    {
        using var temp = new TempDirectory();

        var resolved = ConfigStore.ResolvePath(temp.Path);

        Assert.Contains("easinput", resolved);
        Assert.EndsWith("config.json", resolved);
        Assert.DoesNotContain(temp.Path, resolved);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "easinput-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best effort.
            }
        }
    }
}
