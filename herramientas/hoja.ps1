# Hoja de contactos: une varias imágenes en una cuadrícula con su nombre
param([string[]]$archivos, [string]$salida, [int]$ancho = 680, [int]$cols = 2)
Add-Type -AssemblyName System.Drawing
$imgs = $archivos | ForEach-Object { [System.Drawing.Image]::FromFile($_) }
$alto = ($imgs | ForEach-Object { [int]($_.Height * $ancho / $_.Width) } | Measure-Object -Maximum).Maximum
$filas = [math]::Ceiling($imgs.Count / $cols)
$bmp = New-Object System.Drawing.Bitmap ($ancho * $cols), (($alto + 24) * $filas)
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::White)
$g.InterpolationMode = 'HighQualityBicubic'
$f = New-Object System.Drawing.Font 'Segoe UI', 12, ([System.Drawing.FontStyle]::Bold)
for ($i = 0; $i -lt $imgs.Count; $i++) {
  $x = ($i % $cols) * $ancho; $y = [math]::Floor($i / $cols) * ($alto + 24)
  $h = [int]($imgs[$i].Height * $ancho / $imgs[$i].Width)
  $g.DrawString([IO.Path]::GetFileName($archivos[$i]), $f, [System.Drawing.Brushes]::Red, $x + 4, $y + 2)
  $g.DrawImage($imgs[$i], $x, $y + 24, $ancho - 6, $h)
}
$bmp.Save($salida, [System.Drawing.Imaging.ImageFormat]::Png)
$imgs | ForEach-Object { $_.Dispose() }
"ok $salida"
