# Pruebas automáticas del Capturador: compila, ejecuta una copia aislada y verifica cada función.
# Toma el control del teclado y el mouse unos segundos: no tocar el equipo mientras corre.
$ErrorActionPreference = 'Stop'
# Cierra instancias de pruebas anteriores que hayan quedado abiertas
Get-Process Capturador -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$env:TEMP*" } | Stop-Process -Force
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$raiz = Split-Path -Parent $aqui

& (Join-Path $raiz 'capturador\compilar.ps1') | Out-Host
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tmp = Join-Path $env:TEMP ('capturador-pruebas-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory $tmp | Out-Null
$entrada = Join-Path $tmp 'Entrada.exe'
& $csc /nologo /optimize+ /codepage:65001 /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:$entrada" (Join-Path $aqui 'Entrada.cs') | Out-Host
Copy-Item (Join-Path $raiz 'dist\Capturador.exe') $tmp
$exe = Join-Path $tmp 'Capturador.exe'
$capturas = Join-Path $tmp 'Capturas'
$csv = Join-Path $capturas 'capturas.csv'

$resultados = New-Object System.Collections.ArrayList
function Probar([string]$nombre, [scriptblock]$cuerpo) {
  try { & $cuerpo; [void]$resultados.Add([pscustomobject]@{ Prueba = $nombre; Resultado = 'OK'; Detalle = '' }) }
  catch { [void]$resultados.Add([pscustomobject]@{ Prueba = $nombre; Resultado = 'FALLA'; Detalle = $_.Exception.Message }) }
}
function Afirmar([bool]$cond, [string]$msg) { if (-not $cond) { throw $msg } }
function Entrada([string[]]$a) { $p = Start-Process $entrada -ArgumentList $a -Wait -PassThru -WindowStyle Hidden; return $p.ExitCode }
function Pngs { @(Get-ChildItem $capturas -Filter *.png -ErrorAction SilentlyContinue | Sort-Object Name) }
function Esperar([int]$n, [int]$ms = 5000) {
  $t = [Diagnostics.Stopwatch]::StartNew()
  while ($t.ElapsedMilliseconds -lt $ms) { if (@(Pngs).Count -ge $n) { Start-Sleep -Milliseconds 300; return }; Start-Sleep -Milliseconds 100 }
}
function Tam([string]$f) { $i = [Drawing.Image]::FromFile($f); try { "$($i.Width)x$($i.Height)" } finally { $i.Dispose() } }
function Filas { @(Import-Csv $csv -Delimiter ';' -Encoding UTF8) }
$vs = [Windows.Forms.SystemInformation]::VirtualScreen

# Medir la pantalla con DPI real (el capturador es DPI-aware)
$vsReal = & {
  $p = Start-Process $exe -ArgumentList '--capturar', 'pantalla' -Wait -PassThru
  Afirmar ($p.ExitCode -eq 0) "--capturar devolvió $($p.ExitCode)"
  Tam (Pngs)[0].FullName
}

Probar '1. Captura única por línea de comandos (pantalla completa)' {
  Afirmar (@(Pngs).Count -eq 1) 'no se creó el PNG'
  Afirmar ((Pngs)[0].Name -match '^001_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_pantalla\.png$') "nombre inesperado: $((Pngs)[0].Name)"
  Afirmar (@(Filas).Count -eq 1) 'capturas.csv no tiene 1 fila'
  Afirmar ($vsReal -match '^\d+x\d+$') 'tamaño inválido'
}

$app = Start-Process $exe -PassThru
Start-Sleep -Seconds 2
$ini = Join-Path $tmp 'config.ini'

Probar '2. Inicia, crea config.ini y queda en ejecución' {
  Afirmar (-not $app.HasExited) 'el proceso terminó'
  Afirmar (Test-Path $ini) 'no se creó config.ini'
}

Probar '3. Instancia única: un segundo arranque no abre otra' {
  $p2 = Start-Process $exe -PassThru; $p2.WaitForExit(5000) | Out-Null
  Afirmar $p2.HasExited 'la segunda instancia sigue abierta'
  Afirmar (@(Get-Process Capturador | Where-Object { $_.Path -eq $exe }).Count -eq 1) 'hay más de una instancia'
}

Probar '4. Atajo Ctrl+Alt+S captura la pantalla completa' {
  Entrada 'teclas', 'ctrl', 'alt', 's' | Out-Null; Esperar 2
  Afirmar (@(Pngs).Count -eq 2) "se esperaban 2 PNG, hay $(@(Pngs).Count)"
  Afirmar ((Pngs)[1].Name -like '002_*_pantalla.png') "nombre: $((Pngs)[1].Name)"
  Afirmar ((Tam (Pngs)[1].FullName) -eq $vsReal) 'tamaño distinto a la pantalla'
}

$bloc = Start-Process (Join-Path $env:WINDIR 'system32\notepad.exe') -PassThru
Start-Sleep -Milliseconds 1500
Probar '5. Atajo Ctrl+Alt+V captura solo la ventana activa (Bloc de notas)' {
  Afirmar ((Entrada 'mover', $bloc.Id, '200', '150', '640', '420') -eq 0) 'no se pudo mover el Bloc de notas'
  Start-Sleep -Milliseconds 500
  Afirmar ((Entrada 'frente', $bloc.Id) -eq 0) 'el Bloc de notas no quedó en primer plano'
  $marco = (& $entrada marco $bloc.Id).Trim()
  Entrada 'teclas', 'ctrl', 'alt', 'v' | Out-Null; Esperar 3
  Afirmar (@(Pngs).Count -eq 3) "se esperaban 3 PNG, hay $(@(Pngs).Count)"
  Afirmar ((Pngs)[2].Name -like '003_*_ventana-notepad.png') "nombre: $((Pngs)[2].Name)"
  Afirmar ((Tam (Pngs)[2].FullName) -eq $marco) "tamaño $((Tam (Pngs)[2].FullName)) distinto al marco $marco"
  $f = (Filas)[2]
  Afirmar ($f.proceso -eq 'notepad' -and $f.titulo_ventana -match 'Bloc de notas|Notepad') "registro: $($f.proceso) / $($f.titulo_ventana)"
}

Probar '6. Clic en el botón flotante captura sin robar el foco' {
  $c = Get-Content $ini -Encoding UTF8
  $x = [int](($c | Where-Object { $_ -like 'x=*' }) -replace 'x=', '') + 26
  $y = [int](($c | Where-Object { $_ -like 'y=*' }) -replace 'y=', '') + 26
  Entrada 'frente', $bloc.Id | Out-Null
  Entrada 'clic', $x, $y | Out-Null; Esperar 4
  Afirmar (@(Pngs).Count -eq 4) "se esperaban 4 PNG, hay $(@(Pngs).Count)"
  Afirmar ((Filas)[3].proceso -eq 'notepad') "la ventana activa cambió a: $((Filas)[3].proceso)"
}

Probar '7. Atajo Ctrl+Alt+R captura la región arrastrada (300 x 200)' {
  Entrada 'teclas', 'ctrl', 'alt', 'r' | Out-Null; Start-Sleep -Milliseconds 1200
  Entrada 'arrastrar', '100', '100', '400', '300' | Out-Null; Esperar 5
  Afirmar (@(Pngs).Count -eq 5) "se esperaban 5 PNG, hay $(@(Pngs).Count)"
  Afirmar ((Pngs)[4].Name -like '005_*_region.png') "nombre: $((Pngs)[4].Name)"
  Afirmar ((Tam (Pngs)[4].FullName) -eq '300x200') "tamaño $((Tam (Pngs)[4].FullName))"
}

Probar '8. Esc cancela la selección de región sin guardar nada' {
  Entrada 'teclas', 'ctrl', 'alt', 'r' | Out-Null; Start-Sleep -Milliseconds 1200
  Entrada 'teclas', 'esc' | Out-Null; Start-Sleep -Milliseconds 800
  Afirmar (@(Pngs).Count -eq 5) "se guardó algo al cancelar: $(@(Pngs).Count)"
}

Probar '9. La imagen queda en el portapapeles' {
  $img = [Windows.Forms.Clipboard]::GetImage()
  Afirmar ($null -ne $img) 'no hay imagen en el portapapeles'
  Afirmar ("$($img.Width)x$($img.Height)" -eq '300x200') "portapapeles: $($img.Width)x$($img.Height)"
}

Probar '10. Registro capturas.csv completo y numerado' {
  $f = Filas
  Afirmar ($f.Count -eq 5) "filas: $($f.Count)"
  Afirmar ((($f | ForEach-Object { $_.numero }) -join ',') -eq '001,002,003,004,005') 'numeración no consecutiva'
  Afirmar (@($f | Where-Object { -not (Test-Path (Join-Path $capturas $_.archivo)) }).Count -eq 0) 'falta algún archivo del registro'
}

Probar '11. Ctrl+Alt+B oculta y vuelve a mostrar el botón' {
  Entrada 'teclas', 'ctrl', 'alt', 'b' | Out-Null; Start-Sleep -Milliseconds 600
  Afirmar ((Get-Content $ini -Encoding UTF8) -contains 'visible=0') 'no quedó oculto'
  Entrada 'teclas', 'ctrl', 'alt', 'b' | Out-Null; Start-Sleep -Milliseconds 600
  Afirmar ((Get-Content $ini -Encoding UTF8) -contains 'visible=1') 'no volvió a mostrarse'
}

Probar '12. --salir cierra la instancia y conserva la configuración' {
  Start-Process $exe -ArgumentList '--salir' -Wait | Out-Null
  $app.WaitForExit(5000) | Out-Null
  Afirmar $app.HasExited 'la instancia no se cerró'
  Afirmar (@((Get-Content $ini -Encoding UTF8) -match '^x=-?\d+$').Count -eq 1) 'config.ini sin posición'
}

Probar '13. Comando «despues» se ejecuta tras cada captura' {
  (Get-Content $ini -Encoding UTF8) -replace '^despues=.*$', 'despues=echo {archivo}>> hook.txt' | Set-Content $ini -Encoding UTF8
  $app2 = Start-Process $exe -PassThru; Start-Sleep -Seconds 2
  Entrada 'teclas', 'ctrl', 'alt', 's' | Out-Null; Esperar 6; Start-Sleep -Milliseconds 800
  Start-Process $exe -ArgumentList '--salir' -Wait | Out-Null; $app2.WaitForExit(5000) | Out-Null
  $hook = Join-Path $capturas 'hook.txt'
  Afirmar (Test-Path $hook) 'no se ejecutó el comando'
  Afirmar ((Get-Content $hook -Raw) -match '006_.*_pantalla\.png') "contenido: $(Get-Content $hook -Raw)"
}

Probar '14. Sondeo por hover: detecta el botón y descarta una zona inerte; el clic se verifica' {
  $obj = Start-Process $entrada -ArgumentList 'objetivo' -PassThru; Start-Sleep -Seconds 1
  try {
    $p = Start-Process $exe -ArgumentList '--sondear', '410', '370' -Wait -PassThru
    Afirmar ($p.ExitCode -eq 0) "el botón no se detectó (salida $($p.ExitCode))"
    $p = Start-Process $exe -ArgumentList '--sondear', '555', '465' -Wait -PassThru
    Afirmar ($p.ExitCode -eq 1) "la zona inerte se tomó como interactiva (salida $($p.ExitCode))"
    $p = Start-Process $exe -ArgumentList '--clic', '410', '370', '--esperar', '600' -Wait -PassThru
    Afirmar ($p.ExitCode -eq 0) "el clic no produjo cambio (salida $($p.ExitCode))"
    $a = @(Import-Csv (Join-Path $capturas 'acciones.csv') -Delimiter ';' -Encoding UTF8)
    Afirmar ($a.Count -eq 3) "acciones.csv tiene $($a.Count) filas"
    Afirmar ([int]$a[2].zona_ancho -ge 250) "la zona del clic no abarca la ventana: $($a[2].zona_ancho)"
    Afirmar (@(Get-ChildItem $capturas -Filter '*-marcado.png').Count -eq 3) 'faltan imágenes marcadas'
  } finally { if (-not $obj.HasExited) { $obj.Kill() } }
}

if (-not $bloc.HasExited) { $bloc.Kill() }
$resultados | Format-Table -AutoSize -Wrap | Out-String -Width 200 | Out-Host
$fallas = @($resultados | Where-Object { $_.Resultado -ne 'OK' }).Count
"Pantalla: $vsReal · Carpeta de prueba: $tmp"
"$($resultados.Count - $fallas) de $($resultados.Count) pruebas correctas"
exit $fallas
