# Coloca la ventana de Chrome a la izquierda y la de Brave a la derecha (mitad de pantalla cada una)
param([string]$titulo, [int]$alto = 728)
if (-not ('MvWin' -as [type])) {
Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class MvWin { [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int ht,bool r);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int c); [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); }
"@
}
[MvWin]::SetProcessDPIAware() | Out-Null
$pos = @{ chrome = 0; brave = 683 }
foreach ($n in 'chrome', 'brave') {
  $p = Get-Process $n -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like $titulo } | Select-Object -First 1
  if (-not $p) { "sin ventana $n"; continue }
  [MvWin]::ShowWindow($p.MainWindowHandle, 9) | Out-Null; Start-Sleep -Milliseconds 400
  [MvWin]::MoveWindow($p.MainWindowHandle, $pos[$n], 0, 683, $alto, $true) | Out-Null
  [MvWin]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
  "$n -> x=$($pos[$n])"
}
