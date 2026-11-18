param(
  [int]$X = 0, [int]$Y = 0, [int]$W = 400, [int]$H = 200,
  [string]$Out = "capture.png", [int]$Scale = 4
)
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public class Cap {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  public static void Shot(int x, int y, int w, int h, string path, int scale) {
    SetProcessDpiAwarenessContext(new IntPtr(-4)); // PerMonitorV2
    using (var full = new Bitmap(w, h))
    using (var g = Graphics.FromImage(full)) {
      g.CopyFromScreen(x, y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
      using (var big = new Bitmap(w * scale, h * scale))
      using (var g2 = Graphics.FromImage(big)) {
        g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g2.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        g2.DrawImage(full, 0, 0, w * scale, h * scale);
        big.Save(path, ImageFormat.Png);
      }
    }
  }
}
"@
[Cap]::Shot($X, $Y, $W, $H, $Out, $Scale)
Write-Output ("saved " + $Out)
