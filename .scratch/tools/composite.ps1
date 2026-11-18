param(
  [string]$Left, [int]$LX, [int]$LY, [int]$LW, [int]$LH,
  [string]$Right, [int]$RX, [int]$RY, [int]$RW, [int]$RH,
  [string]$Out, [int]$TargetHeight = 240
)
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public class Comp {
  public static void Run(string left, int lx, int ly, int lw, int lh,
                         string right, int rx, int ry, int rw, int rh,
                         string outp, int th) {
    using (var a = new Bitmap(left))
    using (var b = new Bitmap(right)) {
      var ca = a.Clone(new Rectangle(lx, ly, lw, lh), PixelFormat.Format32bppArgb);
      var cb = b.Clone(new Rectangle(rx, ry, rw, rh), PixelFormat.Format32bppArgb);
      int wa = (int)Math.Round(ca.Width * (double)th / ca.Height);
      int wb = (int)Math.Round(cb.Width * (double)th / cb.Height);
      int gap = 40;
      using (var canvas = new Bitmap(wa + gap + wb, th))
      using (var g = Graphics.FromImage(canvas)) {
        g.Clear(Color.FromArgb(255, 245, 245, 245));
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(ca, 0, 0, wa, th);
        g.DrawImage(cb, wa + gap, 0, wb, th);
        canvas.Save(outp, ImageFormat.Png);
      }
      ca.Dispose(); cb.Dispose();
    }
  }
}
"@
[Comp]::Run($Left,$LX,$LY,$LW,$LH,$Right,$RX,$RY,$RW,$RH,$Out,$TargetHeight)
Write-Output ("saved " + $Out)
