Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$ErrorActionPreference = 'Stop'

$el = [System.Windows.Automation.AutomationElement]::FocusedElement
if ($null -eq $el) { Write-Output "FocusedElement = null"; exit }

$c = $el.Current
Write-Output ("focused: name='" + $c.Name + "' class='" + $c.ClassName + "' hwnd=" + $c.NativeWindowHandle + " type=" + $c.ControlType.ProgrammaticName)

try {
  $tp = $el.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)
  Write-Output "TextPattern: OK"
} catch {
  Write-Output ("TextPattern: FAIL -> " + $_.Exception.Message)
  exit
}

try {
  $sel = $tp.GetSelection()
  Write-Output ("selection ranges: " + $sel.Count)
  $i = 0
  foreach ($r in $sel) {
    $txt = ""
    try { $txt = $r.GetText(80) } catch {}
    $clean = $txt -replace "`r", "\r" -replace "`n", "\n"
    Write-Output ("  [" + $i + "] text='" + $clean + "'")
    $rects = $r.GetBoundingRectangles()
    if ($null -eq $rects -or $rects.Count -eq 0) {
      Write-Output "      bounding rects: (none)"
    } else {
      for ($j = 0; $j -lt $rects.Count; $j++) {
        $b = $rects[$j]
        Write-Output ("      rect[" + $j + "] x=" + $b.X + " y=" + $b.Y + " w=" + $b.Width + " h=" + $b.Height)
      }
    }
    $i++
  }
} catch {
  Write-Output ("GetSelection FAIL -> " + $_.Exception.Message)
}

foreach ($p in $el.GetSupportedPatterns()) { Write-Output ("  supports: " + $p.ProgrammaticName) }
