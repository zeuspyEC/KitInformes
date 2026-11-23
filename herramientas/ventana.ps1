# Captura la ventana principal de un proceso (aunque esté detrás de otras) con PrintWindow
param([int]$procId, [string]$ruta, [string]$titulo = "", [switch]$maximizar)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text;
public class W {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  public struct RECT { public int L, T, R, B; }
}
"@
[W]::SetProcessDPIAware() | Out-Null
$p = if ($titulo) { Get-Process | Where-Object { $_.MainWindowTitle -like $titulo } | Select-Object -First 1 } else { Get-Process -Id $procId }
$h = $p.MainWindowHandle
if ($h -eq [IntPtr]::Zero) { throw "Sin ventana" }
if ([W]::IsIconic($h)) { [W]::ShowWindow($h, 9) | Out-Null; Start-Sleep -Milliseconds 600 }
if ($maximizar) { [W]::ShowWindow($h, 3) | Out-Null; Start-Sleep -Milliseconds 1500 }
$r = New-Object W+RECT; [W]::GetWindowRect($h, [ref]$r) | Out-Null
$bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
$g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc()
[W]::PrintWindow($h, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
# Recorta el borde invisible de Windows 10 usando el marco real de DWM
$f = New-Object W+RECT; [W]::DwmGetWindowAttribute($h, 9, [ref]$f, 16) | Out-Null
$crop = New-Object System.Drawing.Rectangle ($f.L - $r.L), ($f.T - $r.T), ($f.R - $f.L), ($f.B - $f.T)
$bmp.Clone($crop, $bmp.PixelFormat).Save($ruta, [System.Drawing.Imaging.ImageFormat]::Png)
"$($p.ProcessName) '$($p.MainWindowTitle)' -> $ruta"
