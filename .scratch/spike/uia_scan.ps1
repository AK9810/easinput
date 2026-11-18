Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class W {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
"@
$ErrorActionPreference = 'Stop'
$hwnd = [W]::GetForegroundWindow()
Write-Output ("foreground hwnd = " + $hwnd)
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
Write-Output ("root: name='" + $root.Current.Name + "' class='" + $root.Current.ClassName + "'")

$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsTextPatternAvailableProperty, $true)
$found = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
Write-Output ("text-pattern-capable descendants: " + $found.Count)
$i = 0
foreach ($e in $found) {
  if ($i -ge 6) { break }
  $cc = $e.Current
  Write-Output ("  [" + $i + "] type=" + $cc.ControlType.ProgrammaticName + " class='" + $cc.ClassName + "' name='" + $cc.Name + "' hwnd=" + $cc.NativeWindowHandle)
  try {
    $tp = $e.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)
    $sel = $tp.GetSelection()
    Write-Output ("       selection ranges=" + $sel.Count)
    foreach ($r in $sel) {
      $rects = $r.GetBoundingRectangles()
      if ($null -eq $rects -or $rects.Count -eq 0) { Write-Output "       rects: none" }
      else { for ($j=0; $j -lt [Math]::Min($rects.Count,3); $j++) { $b=$rects[$j]; Write-Output ("       rect x=" + $b.X + " y=" + $b.Y + " w=" + $b.Width + " h=" + $b.Height) } }
    }
  } catch { Write-Output ("       pattern/selection FAIL: " + $_.Exception.Message) }
  $i++
}
