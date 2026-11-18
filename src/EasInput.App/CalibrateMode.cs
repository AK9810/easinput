using System.IO;
using System.Windows.Media.Imaging;
using EasInput.Core;
using EasInput.Core.Interop;

namespace EasInput.App;

/// <summary>
/// <c>easinput --calibrate</c>: renders the real bubble at 100%, 150% and 200% into PNGs in the
/// current directory, for human comparison against the macOS reference.
/// </summary>
internal static class CalibrateMode
{
    private static readonly (int Percent, double Scale)[] Scales =
    {
        (100, 1.0),
        (150, 1.5),
        (200, 2.0),
    };

    public static void Run()
    {
        NativeMethods.AttachConsole(-1);
        var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(output);

        var config = ConfigStore.Load(ConfigStore.ResolvePath());
        var bubble = new BubbleWindow();
        foreach (var (percent, scale) in Scales)
        {
            var bitmap = bubble.Render(InputMode.Chinese, config.ShortCodes.Chinese, config, scale);
            var path = Path.GetFullPath($"calibrate-{percent}.png");
            using (var stream = File.Create(path))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }

            Console.WriteLine($"calibrate: {path} {bitmap.PixelWidth}x{bitmap.PixelHeight}");
        }

        NativeMethods.FreeConsole();
    }
}
