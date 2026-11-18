param([string]$Out)
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public class Strip {
  public static void Run(string outp) {
    string[] paths = {
      @"F:\worksapce\easinput\.scratch\ref\macos_indicator_on.png",
      @"F:\worksapce\easinput\.scratch\ref\ours_cn.png",
      @"F:\worksapce\easinput\.scratch\ref\ours_a.png",
      @"F:\worksapce\easinput\.scratch\ref\ours_A.png"
    };
    Rectangle[] crops = {
      new Rectangle(196,336,84,66),
      new Rectangle(8,6,48,38),
      new Rectangle(8,6,48,38),
      new Rectangle(8,6,48,38)
    };
    int th = 240, gap = 30;
    var parts = new Bitmap[paths.Length];
    int total = gap * (paths.Length + 1);
    for (int i = 0; i < paths.Length; i++) {
      using (var src = new Bitmap(paths[i])) {
        var c = src.Clone(crops[i], PixelFormat.Format32bppArgb);
        int w = (int)Math.Round(c.Width * (double)th / c.Height);
        parts[i] = new Bitmap(w, th);
        using (var g = Graphics.FromImage(parts[i])) {
          g.InterpolationMode = InterpolationMode.HighQualityBicubic;
          g.PixelOffsetMode = PixelOffsetMode.HighQuality;
          g.DrawImage(c, 0, 0, w, th);
        }
        c.Dispose();
        total += w;
      }
    }
    using (var canvas = new Bitmap(total, th + 40))
    using (var g = Graphics.FromImage(canvas)) {
      g.Clear(Color.FromArgb(255, 250, 250, 250));
      int x = gap;
      foreach (var p in parts) { g.DrawImage(p, x, 20); x += p.Width + gap; }
      canvas.Save(outp, ImageFormat.Png);
    }
    foreach (var p in parts) p.Dispose();
  }
}
"@
[Strip]::Run($Out)
Write-Output ("saved " + $Out)
