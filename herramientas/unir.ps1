# Une dos o más capturas de ventana lado a lado, a la misma altura, sin rótulos
param([string[]]$archivos, [string]$salida, [int]$alto = 720)
Add-Type -AssemblyName System.Drawing
$imgs = $archivos | ForEach-Object { [System.Drawing.Image]::FromFile($_) }
$anchos = $imgs | ForEach-Object { [int]($_.Width * $alto / $_.Height) }
$total = ($anchos | Measure-Object -Sum).Sum + 8 * ($imgs.Count - 1)
$bmp = New-Object System.Drawing.Bitmap ([int]$total), ([int]$alto)
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::FromArgb(60,60,60)); $g.InterpolationMode = 'HighQualityBicubic'
$x = 0
for ($i = 0; $i -lt $imgs.Count; $i++) { $g.DrawImage($imgs[$i], $x, 0, $anchos[$i], $alto); $x += $anchos[$i] + 8 }
$bmp.Save($salida, [System.Drawing.Imaging.ImageFormat]::Png); $imgs | ForEach-Object { $_.Dispose() }
"ok $salida ($total x $alto)"
