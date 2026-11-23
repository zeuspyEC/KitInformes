# Clic en coordenadas de pantalla SOLO si la ventana indicada está en primer plano
param([string]$titulo, [int]$x, [int]$y, [switch]$doble)
$ErrorActionPreference = 'Stop'
if (-not ('Mou' -as [type])) {
Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class Mou { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern void mouse_event(int f,int x,int y,int d,int e);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, int f, int e);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int c); }
"@
}
[Mou]::SetProcessDPIAware() | Out-Null
$p = Get-Process | Where-Object { $_.MainWindowTitle -like $titulo } | Select-Object -First 1
if (-not $p) { throw "sin ventana $titulo" }
$h = $p.MainWindowHandle
if ([Mou]::GetForegroundWindow() -ne $h) { [Mou]::ShowWindow($h,3) | Out-Null; [Mou]::keybd_event(0x12,0,0,0); [Mou]::SetForegroundWindow($h) | Out-Null; [Mou]::keybd_event(0x12,0,2,0); Start-Sleep -Milliseconds 500 }
if ([Mou]::GetForegroundWindow() -ne $h) { throw "ventana no en primer plano" }
[Mou]::SetCursorPos($x,$y); Start-Sleep -Milliseconds 150
$n = if ($doble) {2} else {1}
for ($i=0;$i -lt $n;$i++){ [Mou]::mouse_event(2,0,0,0,0); [Mou]::mouse_event(4,0,0,0,0); Start-Sleep -Milliseconds 80 }
"clic $x,$y"
