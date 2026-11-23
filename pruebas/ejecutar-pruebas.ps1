# Pruebas automáticas de KitInformes: compila dist\KitInformes.exe, ejecuta una copia aislada y verifica cada función.
# Toma el control del teclado y el mouse alrededor de un minuto: no tocar el equipo mientras corre.
# Solo usa ventanas de prueba propias (pruebas\Entrada.cs), nunca programas del usuario.
$ErrorActionPreference = 'Stop'
Get-Process KitInformes, Entrada -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*\pruebas\tmp\*' } | Stop-Process -Force
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$raiz = Split-Path -Parent $aqui

& (Join-Path $raiz 'construir.ps1') | Out-Host
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
# Copia aislada dentro del proyecto (pruebas\tmp, ignorada por git)
$tmp = Join-Path $aqui ('tmp\' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force $tmp | Out-Null
$entrada = Join-Path $tmp 'Entrada.exe'
& $csc /nologo /optimize+ /codepage:65001 /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:$entrada" (Join-Path $aqui 'Entrada.cs') | Out-Host
Copy-Item (Join-Path $raiz 'dist\KitInformes.exe') $tmp
$exe = Join-Path $tmp 'KitInformes.exe'
$capturas = Join-Path $tmp 'Capturas'
$csv = Join-Path $capturas 'capturas.csv'
$ini = Join-Path $tmp 'config.ini'

$resultados = New-Object System.Collections.ArrayList
function Probar([string]$nombre, [scriptblock]$cuerpo) {
  try { & $cuerpo; [void]$resultados.Add([pscustomobject]@{ Prueba = $nombre; Resultado = 'OK'; Detalle = '' }) }
  catch { [void]$resultados.Add([pscustomobject]@{ Prueba = $nombre; Resultado = 'FALLA'; Detalle = $_.Exception.Message }) }
}
function Afirmar([bool]$cond, [string]$msg) { if (-not $cond) { throw $msg } }
# Start-Process de PowerShell 5 no entrecomilla los argumentos con espacios: se hace aquí
function Args([string[]]$a) { @($a | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) }
function Entrada([string[]]$a) { $p = Start-Process $entrada -ArgumentList (Args $a) -Wait -PassThru -WindowStyle Hidden; return $p.ExitCode }
function Kit([string[]]$a) { $p = Start-Process $exe -ArgumentList (Args $a) -Wait -PassThru -WindowStyle Hidden; return $p.ExitCode }
function Pngs { @(Get-ChildItem $capturas -Filter *.png -ErrorAction SilentlyContinue | Sort-Object Name) }
function Esperar([int]$n, [int]$ms = 5000) {
  $t = [Diagnostics.Stopwatch]::StartNew()
  while ($t.ElapsedMilliseconds -lt $ms) { if (@(Pngs).Count -ge $n) { Start-Sleep -Milliseconds 300; return }; Start-Sleep -Milliseconds 100 }
}
function Tam([string]$f) { $i = [Drawing.Image]::FromFile($f); try { "$($i.Width)x$($i.Height)" } finally { $i.Dispose() } }
function Filas { @(Import-Csv $csv -Delimiter ';' -Encoding UTF8) }
function Ventana([string]$tipo) { $p = Start-Process $entrada -ArgumentList $tipo -PassThru; Start-Sleep -Milliseconds 1200; return $p }
function Cerrar($p) { if ($p -and -not $p.HasExited) { $p.Kill() } }
function Ini([string]$clave, [string]$valor) { (Get-Content $ini -Encoding UTF8) -replace "^$clave=.*$", "$clave=$valor" | Set-Content $ini -Encoding UTF8 }

# Medir la pantalla con DPI real (el programa es DPI-aware)
$vsReal = & {
  $p = Start-Process $exe -ArgumentList '--capturar', 'pantalla' -Wait -PassThru
  Afirmar ($p.ExitCode -eq 0) "--capturar devolvió $($p.ExitCode)"
  Tam (Pngs)[0].FullName
}

Probar '1. Captura única por línea de comandos (pantalla completa)' {
  Afirmar (@(Pngs).Count -eq 1) 'no se creó el PNG'
  Afirmar ((Pngs)[0].Name -match '^001_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_pantalla\.png$') "nombre inesperado: $((Pngs)[0].Name)"
  Afirmar (@(Filas).Count -eq 1) 'capturas.csv no tiene 1 fila'
}

$app = Start-Process $exe -PassThru
Start-Sleep -Seconds 2

Probar '2. Inicia, crea config.ini y queda en ejecución' {
  Afirmar (-not $app.HasExited) 'el proceso terminó'
  Afirmar (Test-Path $ini) 'no se creó config.ini'
  Afirmar ((Get-Content $ini -Encoding UTF8) -contains 'modo=capturar') 'el modo inicial no es «capturar»'
}

Probar '3. Instancia única: un segundo arranque no abre otra' {
  $p2 = Start-Process $exe -PassThru; $p2.WaitForExit(5000) | Out-Null
  Afirmar $p2.HasExited 'la segunda instancia sigue abierta'
  Afirmar (@(Get-Process KitInformes | Where-Object { $_.Path -eq $exe }).Count -eq 1) 'hay más de una instancia'
}

Probar '4. Atajo Ctrl+Alt+S captura la pantalla completa' {
  Entrada 'teclas', 'ctrl', 'alt', 's' | Out-Null; Esperar 2
  Afirmar (@(Pngs).Count -eq 2) "se esperaban 2 PNG, hay $(@(Pngs).Count)"
  Afirmar ((Pngs)[1].Name -like '002_*_pantalla.png') "nombre: $((Pngs)[1].Name)"
  Afirmar ((Tam (Pngs)[1].FullName) -eq $vsReal) 'tamaño distinto a la pantalla'
}

$obj = Ventana 'objetivo'
Probar '5. Atajo Ctrl+Alt+V captura solo la ventana activa (ventana de prueba)' {
  Afirmar ((Entrada 'frente', $obj.Id) -eq 0) 'la ventana de prueba no quedó en primer plano'
  Entrada 'teclas', 'ctrl', 'alt', 'v' | Out-Null; Esperar 3
  Afirmar (@(Pngs).Count -eq 3) "se esperaban 3 PNG, hay $(@(Pngs).Count)"
  Afirmar ((Pngs)[2].Name -like '003_*_ventana-Entrada.png') "nombre: $((Pngs)[2].Name)"
  Afirmar ((Tam (Pngs)[2].FullName) -eq '300x200') "tamaño $((Tam (Pngs)[2].FullName)) distinto de 300x200"
  $f = (Filas)[2]
  Afirmar ($f.proceso -eq 'Entrada' -and $f.titulo_ventana -eq 'Objetivo de prueba') "registro: $($f.proceso) / $($f.titulo_ventana)"
}

Probar '6. Clic en el botón flotante captura sin robar el foco' {
  $c = Get-Content $ini -Encoding UTF8
  $x = [int](($c | Where-Object { $_ -like 'x=*' }) -replace 'x=', '') + 26
  $y = [int](($c | Where-Object { $_ -like 'y=*' }) -replace 'y=', '') + 26
  Entrada 'frente', $obj.Id | Out-Null
  Entrada 'clic', $x, $y | Out-Null; Esperar 4
  Afirmar (@(Pngs).Count -eq 4) "se esperaban 4 PNG, hay $(@(Pngs).Count)"
  Afirmar ((Filas)[3].proceso -eq 'Entrada') "la ventana activa cambió a: $((Filas)[3].proceso)"
}
Cerrar $obj

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

Probar '12. Configuración: la ventana se abre y el propio programa elige «Capturar y analizar» y guarda' {
  Start-Process $exe -ArgumentList '--configuracion' -Wait | Out-Null; Start-Sleep -Milliseconds 1500
  Afirmar ((Kit 'elementos', '--ventana', 'Configuración - KitInformes') -eq 0) 'no se abrió la ventana de configuración'
  Afirmar ((Kit 'clic-nombre', 'Capturar y analizar', '--ventana', 'Configuración - KitInformes') -eq 0) 'no se pudo elegir el modo'
  Afirmar ((Kit 'clic-nombre', 'Guardar', '--tipo', 'botón', '--ventana', 'Configuración - KitInformes') -eq 0) 'no se pudo guardar'
  Start-Sleep -Milliseconds 600
  Afirmar ((Get-Content $ini -Encoding UTF8) -contains 'modo=analizar') 'config.ini no quedó en modo analizar'
}

Probar '13. Modo analizar: limpia la ventana emergente, captura y deja el análisis' {
  $dlg = Ventana 'dialogo'
  try {
    Afirmar ((Entrada 'frente', $dlg.Id) -eq 0) 'el diálogo no quedó en primer plano'
    $antes = @(Pngs).Count
    Entrada 'teclas', 'ctrl', 'alt', 's' | Out-Null; Esperar ($antes + 1) 8000; Start-Sleep -Milliseconds 1500
    Afirmar ($dlg.HasExited) 'la ventana emergente no se cerró con «No, gracias»'
    $png = (Pngs)[-1]
    $md = Join-Path $capturas ($png.BaseName + '_analisis.md')
    Afirmar (Test-Path $md) "no se creó $($png.BaseName)_analisis.md"
    Afirmar ((Get-Content $md -Raw -Encoding UTF8) -match 'Se observa la ventana') 'el análisis no tiene resumen'
  } finally { Cerrar $dlg }
  Ini 'modo' 'capturar'
}

Probar '14. --salir cierra la instancia y conserva la configuración' {
  Start-Process $exe -ArgumentList '--salir' -Wait | Out-Null
  $app.WaitForExit(5000) | Out-Null
  Afirmar $app.HasExited 'la instancia no se cerró'
  Afirmar (@((Get-Content $ini -Encoding UTF8) -match '^x=-?\d+$').Count -eq 1) 'config.ini sin posición'
}

Probar '15. Comando «despues» se ejecuta tras cada captura' {
  Ini 'despues' 'echo {archivo}>> hook.txt'
  $app2 = Start-Process $exe -PassThru; Start-Sleep -Seconds 2
  $antes = @(Pngs).Count
  Entrada 'teclas', 'ctrl', 'alt', 's' | Out-Null; Esperar ($antes + 1) 6000; Start-Sleep -Milliseconds 800
  Start-Process $exe -ArgumentList '--salir' -Wait | Out-Null; $app2.WaitForExit(5000) | Out-Null
  $hook = Join-Path $capturas 'hook.txt'
  Afirmar (Test-Path $hook) 'no se ejecutó el comando'
  Afirmar ((Get-Content $hook -Raw) -match '_pantalla\.png') "contenido: $(Get-Content $hook -Raw)"
}

Probar '16. Sondeo por hover: detecta el botón y descarta una zona inerte; el clic se verifica' {
  $o = Ventana 'objetivo'
  $acc = Join-Path $capturas 'acciones.csv'
  $previas = if (Test-Path $acc) { @(Import-Csv $acc -Delimiter ';' -Encoding UTF8).Count } else { 0 }
  try {
    Afirmar ((Kit '--sondear', '410', '370') -eq 0) 'el botón no se detectó'
    Afirmar ((Kit '--sondear', '555', '465') -eq 1) 'la zona inerte se tomó como interactiva'
    Afirmar ((Kit '--clic', '410', '370', '--esperar', '600') -eq 0) 'el clic no produjo cambio'
    $a = @(Import-Csv $acc -Delimiter ';' -Encoding UTF8)
    Afirmar ($a.Count -eq $previas + 3) "acciones.csv: se esperaban 3 filas nuevas, hay $($a.Count - $previas)"
    Afirmar ([double]::Parse($a[-1].porcentaje_local, [Globalization.CultureInfo]::InvariantCulture) -ge 50) "cambio local: $($a[-1].porcentaje_local) %"
  } finally { Cerrar $o }
}

Probar '17. Elementos: lee los botones de una ventana con nombre y posición' {
  $o = Ventana 'objetivo'
  try {
    Afirmar ((Kit 'elementos', '--ventana', 'Objetivo de prueba') -eq 0) 'no encontró elementos'
    $csvE = Get-ChildItem $capturas -Filter '*_elementos.csv' | Sort-Object Name | Select-Object -Last 1
    $f = @(Import-Csv $csvE.FullName -Delimiter ';' -Encoding UTF8 | Where-Object { $_.nombre -eq 'Probar' })
    Afirmar ($f.Count -eq 1) 'no aparece el botón «Probar»'
    Afirmar ([math]::Abs([int]$f[0].centro_x - 410) -le 3 -and [math]::Abs([int]$f[0].centro_y - 370) -le 3) "posición ($($f[0].centro_x), $($f[0].centro_y))"
  } finally { Cerrar $o }
}

Probar '18. Clic por nombre con verificación del cambio' {
  $o = Ventana 'objetivo'
  try {
    Afirmar ((Kit 'clic-nombre', 'probar', '--ventana', 'Objetivo de prueba') -eq 0) 'el clic por nombre no produjo cambio'
    Afirmar ((Kit 'clic-nombre', 'No existe', '--ventana', 'Objetivo de prueba') -eq 4) 'un nombre inexistente no devolvió 4'
  } finally { Cerrar $o }
}

Probar '19. Analizar: describe la ventana (título, botones y textos)' {
  $o = Ventana 'objetivo'
  try {
    Afirmar ((Kit 'analizar', '--ventana', 'Objetivo de prueba') -eq 0) 'analizar falló'
    $md = Get-ChildItem $capturas -Filter '*_analisis.md' | Sort-Object Name | Select-Object -Last 1
    $t = Get-Content $md.FullName -Raw -Encoding UTF8
    Afirmar ($t -match 'Se observa la ventana «Objetivo de prueba» \(Entrada\)') 'resumen sin la ventana'
    Afirmar ($t -match 'Contiene 1 botón' -and $t -match '\| botón \| Probar \|') 'no describe el botón «Probar»'
  } finally { Cerrar $o }
}

Probar '20. Limpiar: cierra una ventana emergente con «No, gracias» sin mover el mouse' {
  $dlg = Ventana 'dialogo'
  try {
    $cursor = [Windows.Forms.Cursor]::Position
    Afirmar ((Kit 'limpiar', '--ventana', 'Diálogo de prueba') -eq 0) 'no cerró nada'
    $dlg.WaitForExit(3000) | Out-Null
    Afirmar $dlg.HasExited 'la ventana emergente sigue abierta'
    Afirmar ([Windows.Forms.Cursor]::Position -eq $cursor) 'se movió el mouse'
    Afirmar ((Kit 'limpiar', '--ventana', 'Objetivo de prueba') -eq 1) 'sin ventanas emergentes debe devolver 1'
  } finally { Cerrar $dlg }
}

Probar '21. Guion completo con registro en voz impersonal' {
  $o = Ventana 'objetivo'
  try {
    $g = Join-Path $tmp 'guion.txt'
    "# Guion de prueba`r`nventana Objetivo de prueba`r`nelementos`r`nverificar Probar | botón`r`nclic-nombre Probar | botón`r`ncapturar Se capturó el resultado" | Set-Content $g -Encoding UTF8
    Afirmar ((Kit 'guion', $g) -eq 0) 'el guion falló'
    $reg = Get-ChildItem $capturas -Directory -Filter 'guion_*' | Select-Object -Last 1
    $md = Get-Content (Join-Path $reg.FullName 'registro.md') -Raw -Encoding UTF8
    Afirmar ($md -match 'todos los pasos correctos\*\* \(5\)') 'el registro no indica 5 pasos correctos'
    Afirmar ($md -match 'Se hizo clic en el botón «Probar» y la pantalla cambió') 'falta la redacción impersonal del clic'
  } finally { Cerrar $o }
}

$resultados | Format-Table -AutoSize -Wrap | Out-String -Width 200 | Out-Host
$fallas = @($resultados | Where-Object { $_.Resultado -ne 'OK' }).Count
"Pantalla: $vsReal · Carpeta de prueba: $tmp"
"$($resultados.Count - $fallas) de $($resultados.Count) pruebas correctas"
exit $fallas
