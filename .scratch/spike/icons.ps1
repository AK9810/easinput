Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class Ico {
  [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
  public static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint n);
  [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
}
"@
$targets = @("C:\Windows\system32\wetype_tip.dll", "C:\Program Files\Tencent\WeType\WeType.exe")
foreach ($t in $targets) {
  if (-not (Test-Path $t)) { Write-Output ("MISSING: " + $t); continue }
  $n = [Ico]::ExtractIconEx($t, -1, $null, $null, 0)
  Write-Output ("FILE " + $t + "  icon count = " + $n)
  if ($n -eq 0) { continue }
  $large = New-Object IntPtr[] 1
  $small = New-Object IntPtr[] 1
  $got = [Ico]::ExtractIconEx($t, 0, $large, $small, 1)
  if ($got -gt 0 -and $large[0] -ne [IntPtr]::Zero) {
    $icon = [System.Drawing.Icon]::FromHandle($large[0])
    $bmp = $icon.ToBitmap()
    $out = Join-Path (Get-Location) ("icon_" + [IO.Path]::GetFileNameWithoutExtension($t) + ".png")
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output ("  saved " + $out + "  " + $bmp.Width + "x" + $bmp.Height)
    $bmp.Dispose(); $icon.Dispose()
    [void][Ico]::DestroyIcon($large[0]); [void][Ico]::DestroyIcon($small[0])
  } else { Write-Output "  extract index 0 failed" }
}
