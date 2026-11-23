// Renderiza páginas de un PDF a PNG para revisarlas
const { pdfToPng } = require('pdf-to-png-converter');
const [pdf, out, paginas] = process.argv.slice(2);
pdfToPng(pdf, { outputFolder: out, viewportScale: 0.6, pagesToProcess: paginas ? paginas.split(',').map(Number) : undefined, outputFileMaskFunc: (n) => `p${String(n).padStart(2, '0')}.png` })
  .then((r) => console.log(r.length, 'páginas'));
