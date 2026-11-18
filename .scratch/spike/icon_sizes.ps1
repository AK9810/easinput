Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class Ico2 {
  [DllImport("user32.dll", CharSet=CharSet.Unicode)]
  public static extern int PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, int[] ids, int n, int flags);
  [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
}
"@
Add-Type -AssemblyName System.Drawing
$f = "C:\Windows\system32\wetype_tip.dll"
foreach ($size in 16,20,24,32,40,48,64,256) {
  $h = New-Object IntPtr[] 1
  $id = New-Object int[] 1
  $n = [Ico2]::PrivateExtractIcons($f, 0, $size, $size, $h, $id, 1, 0)
  if ($n -gt 0 -and $h[0] -ne [IntPtr]::Zero) {
    $icon = [System.Drawing.Icon]::FromHandle($h[0])
    Write-Output ("requested " + $size + " -> got " + $icon.Width + "x" + $icon.Height)
    $icon.Dispose(); [void][Ico2]::DestroyIcon($h[0])
  } else { Write-Output ("requested " + $size + " -> none") }
}
