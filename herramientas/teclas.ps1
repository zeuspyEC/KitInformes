# Envía teclas a una ventana por título SOLO si esa ventana está en primer plano
param([string]$titulo, [string[]]$teclas, [int]$pausa = 700)
$ErrorActionPreference = 'Stop'
if (-not ('FgWin' -as [type])) {
Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class FgWin { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int c);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, int f, int e); }
"@
}
$p = Get-Process | Where-Object { $_.MainWindowTitle -like $titulo } | Select-Object -First 1
if (-not $p) { throw "sin ventana $titulo" }
$h = $p.MainWindowHandle
[FgWin]::ShowWindow($h, 3) | Out-Null
[FgWin]::keybd_event(0x12,0,0,0); [FgWin]::SetForegroundWindow($h) | Out-Null; [FgWin]::keybd_event(0x12,0,2,0)
Start-Sleep -Milliseconds 700
$ws = New-Object -ComObject WScript.Shell
foreach ($t in $teclas) {
  if ([FgWin]::GetForegroundWindow() -ne $h) { throw "La ventana '$titulo' no está en primer plano; no se envían teclas" }
  $ws.SendKeys($t); Start-Sleep -Milliseconds $pausa
}
"enviado a '$($p.MainWindowTitle)'"
