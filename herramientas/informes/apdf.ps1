# Abre cada .docx con Word, actualiza el índice y los campos, guarda y exporta a PDF
param([string[]]$docx)
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {
  foreach ($f in $docx) {
    $doc = $word.Documents.Open($f, $false, $false)
    foreach ($t in $doc.TablesOfContents) { $t.Update() }
    $doc.Fields.Update() | Out-Null
    foreach ($t in $doc.TablesOfContents) { $t.Update() }
    $doc.Save()
    $pdf = [IO.Path]::ChangeExtension($f, '.pdf')
    $doc.ExportAsFixedFormat($pdf, 17)   # 17 = wdExportFormatPDF
    "$pdf  ($($doc.ComputeStatistics(2)) páginas)"
    $doc.Close($false)
  }
} finally { $word.Quit() }
