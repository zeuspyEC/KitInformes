# Compila Capturador.exe (portable, un solo archivo) con el csc de .NET Framework incluido en Windows
$ErrorActionPreference = 'Stop'
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path (Split-Path -Parent $aqui) 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$src = (Get-ChildItem $aqui -Filter *.cs | Sort-Object Name).FullName
$exe = Join-Path $dist 'Capturador.exe'
$ico = Join-Path $dist 'capturador.ico'
# Si el Capturador compilado está abierto, se cierra para poder reemplazar el .exe
Get-Process Capturador -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | ForEach-Object { Start-Process $exe -ArgumentList '--salir' -Wait; if (-not $_.WaitForExit(5000)) { $_.Kill() } }
$wpf = Join-Path (Split-Path -Parent $csc) 'WPF'
# UI Automation (accesibilidad de Windows) para leer botones, enlaces y campos sin IA
$refs = '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
  "/r:$wpf\UIAutomationClient.dll", "/r:$wpf\UIAutomationTypes.dll", "/r:$wpf\WindowsBase.dll"
# 1) compilación sin ícono para generar el .ico con el mismo dibujo del botón
& $csc /nologo /target:winexe /optimize+ /codepage:65001 $refs "/out:$exe" $src
if ($LASTEXITCODE -ne 0) { throw 'Error de compilación' }
$p = Start-Process $exe -ArgumentList '--exportar-icono', "`"$ico`"" -Wait -PassThru
if ($p.ExitCode -ne 0 -or -not (Test-Path $ico)) { throw 'No se generó el ícono' }
# 2) compilación final con el ícono incrustado
& $csc /nologo /target:winexe /optimize+ /codepage:65001 $refs "/win32icon:$ico" "/out:$exe" $src
if ($LASTEXITCODE -ne 0) { throw 'Error de compilación' }
"OK -> $exe ($([math]::Round((Get-Item $exe).Length / 1KB)) KB)"
