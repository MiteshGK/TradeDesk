# Captures a window to PNG (used to produce the README screenshots).
param([string]$ProcessName, [string]$OutFile)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Win {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$p = Get-Process $ProcessName | Where-Object MainWindowHandle -ne 0 | Select-Object -First 1
if (-not $p) { throw "No window for $ProcessName" }
$h = $p.MainWindowHandle
[Win]::ShowWindow($h, 9) | Out-Null
Start-Sleep -Milliseconds 800
$r = New-Object Win+RECT; [Win]::GetWindowRect($h, [ref]$r) | Out-Null
$bmp = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc(); [Win]::PrintWindow($h, $hdc, 2) | Out-Null; $g.ReleaseHdc($hdc)
$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"saved $OutFile"
