# Compila dist\KitInformes.exe: un único ejecutable portable (sin instalación ni dependencias externas;
# usa solo .NET Framework 4.x, que viene con Windows 10/11) con el csc incluido en Windows.
# Uso: powershell -ExecutionPolicy Bypass -File construir.ps1
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $raiz 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$wpf = Join-Path (Split-Path -Parent $csc) 'WPF'
# UI Automation (accesibilidad de Windows) para leer textos, botones y campos sin IA
$refs = '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
  "/r:$wpf\UIAutomationClient.dll", "/r:$wpf\UIAutomationTypes.dll", "/r:$wpf\WindowsBase.dll"
$fuentes = @(Get-ChildItem (Join-Path $raiz 'capturador'), (Join-Path $raiz 'automatizador') -Filter *.cs | Sort-Object FullName).FullName
$exe = Join-Path $dist 'KitInformes.exe'
$ico = Join-Path $dist 'kitinformes.ico'

# Si está abierto, se cierra para poder reemplazar el .exe
Get-Process KitInformes -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | ForEach-Object {
  Start-Process $exe -ArgumentList '--salir' -Wait
  if (-not $_.WaitForExit(5000)) { $_.Kill() }
}

function Compilar([string[]]$extra) {
  & $csc /nologo /target:winexe /optimize+ /codepage:65001 $refs $extra "/out:$exe" $fuentes
  if ($LASTEXITCODE -ne 0) { throw 'Error de compilación' }
}
Compilar @()                                     # 1) sin ícono, para exportar el .ico con el dibujo del botón
$p = Start-Process $exe -ArgumentList '--exportar-icono', "`"$ico`"" -Wait -PassThru
if ($p.ExitCode -ne 0 -or -not (Test-Path $ico)) { throw 'No se generó el ícono' }
Compilar @("/win32icon:$ico")                    # 2) final con el ícono incrustado
"OK -> $exe ($([math]::Round((Get-Item $exe).Length / 1KB)) KB)"
