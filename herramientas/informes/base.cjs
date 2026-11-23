// Utilidades comunes para los informes de laboratorio (Word, Times New Roman 12, tercera persona)
const fs = require('fs');
const d = require('docx');
const { Document, Packer, Paragraph, TextRun, ImageRun, Table, TableRow, TableCell, WidthType, AlignmentType,
  HeadingLevel, TableOfContents, Footer, PageNumber, PageBreak, BorderStyle, ShadingType } = d;

const TNR = 'Times New Roman';
// Carpeta raíz de la asignatura (se puede cambiar con la variable de entorno INFORMES_RAIZ)
const RAIZ = process.env.INFORMES_RAIZ || 'C:/Users/Erick/Documents/LabsAplicacionesWebAvanzadas/';
const CAPTURAS_USUARIO = RAIZ + 'CAPTURAS/';

function nuevoInforme({ numero, titulo, subtitulo, repos, fecha = new Date().toLocaleDateString('es-EC', { day: 'numeric', month: 'long', year: 'numeric' }) }) {
  const children = [];
  let figura = 0, tabla = 0;
  const run = (t, o = {}) => new TextRun({ text: t, font: TNR, size: 24, ...o });
  const enlace = (url, size = 24) => new d.ExternalHyperlink({ link: url, children: [new TextRun({ text: url, font: TNR, size, color: '0563C1', underline: {} })] });
  // Texto con **negrita**, `código` y [[url]]
  const partes = (texto, size = 24) => texto.split(/(\*\*[^*]+\*\*|`[^`]+`|\[\[[^\]]+\]\])/).filter(Boolean).map((s) =>
    s.startsWith('**') ? run(s.slice(2, -2), { bold: true, size })
      : s.startsWith('`') ? new TextRun({ text: s.slice(1, -1), font: 'Consolas', size: size - 3 })
        : s.startsWith('[[') ? enlace(s.slice(2, -2), size)
          : run(s, { size }));
  const P = (t, o = {}) => children.push(new Paragraph({ children: partes(t), alignment: AlignmentType.JUSTIFIED, spacing: { after: 120, line: 360 }, ...o }));
  const H1 = (t) => children.push(new Paragraph({ heading: HeadingLevel.HEADING_1, children: [run(t, { bold: true, size: 26 })] }));
  const H2 = (t) => children.push(new Paragraph({ heading: HeadingLevel.HEADING_2, children: [run(t, { bold: true })] }));
  const L = (t, nivel = 0) => children.push(new Paragraph({ children: partes(t), bullet: { level: nivel }, alignment: AlignmentType.JUSTIFIED, spacing: { after: 60, line: 360 } }));
  const SALTO = () => children.push(new Paragraph({ children: [new PageBreak()] }));

  function FIG(archivo, texto, maxW = 560, maxH = 600) {
    const b = fs.readFileSync(archivo);
    const w = b.readUInt32BE(16), h = b.readUInt32BE(20);
    const k = Math.min(maxW / w, maxH / h, 1);
    figura++;
    children.push(new Paragraph({ alignment: AlignmentType.CENTER, keepNext: true, spacing: { before: 120, after: 60 },
      children: [new ImageRun({ type: 'png', data: b, transformation: { width: Math.round(w * k), height: Math.round(h * k) } })] }));
    children.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 200 },
      children: [run(`Figura ${figura}. `, { bold: true, italics: true, size: 20 }), run(texto, { italics: true, size: 20 })] }));
  }
  function CODE(titulo, codigo) {
    children.push(new Paragraph({ keepNext: true, spacing: { before: 120, after: 40 }, children: [run(titulo, { bold: true, size: 22 })] }));
    const lineas = codigo.replace(/\r/g, '').replace(/\n$/, '').split('\n');
    lineas.forEach((l, i) => children.push(new Paragraph({
      keepNext: i < lineas.length - 1, spacing: { after: 0, line: 240 },
      shading: { type: ShadingType.CLEAR, fill: 'F3F3F3', color: 'auto' },
      children: [new TextRun({ text: l.length ? l : ' ', font: 'Consolas', size: 18 })],
    })));
    children.push(new Paragraph({ spacing: { after: 160 }, children: [] }));
  }
  function TABLA(titulo, encabezado, filas, anchos) {
    tabla++;
    children.push(new Paragraph({ keepNext: true, alignment: AlignmentType.CENTER, spacing: { before: 120, after: 60 },
      children: [run(`Tabla ${tabla}. `, { bold: true, italics: true, size: 20 }), run(titulo, { italics: true, size: 20 })] }));
    const borde = { style: BorderStyle.SINGLE, size: 4, color: '999999' };
    const bordes = { top: borde, bottom: borde, left: borde, right: borde };
    const n = encabezado.length;
    const tw = (anchos || encabezado.map(() => 100 / n)).map((p) => Math.round((9070 * p) / 100));
    const celda = (t, cab, i) => new TableCell({
      borders: bordes, width: { size: tw[i], type: WidthType.DXA },
      shading: cab ? { type: ShadingType.CLEAR, fill: 'D9D9D9', color: 'auto' } : undefined,
      margins: { top: 60, bottom: 60, left: 100, right: 100 },
      children: [new Paragraph({ children: cab ? [run(t, { bold: true, size: 22 })] : partes(t, 22) })],
    });
    children.push(new Table({
      width: { size: 9070, type: WidthType.DXA }, columnWidths: tw, layout: d.TableLayoutType.FIXED,
      rows: [new TableRow({ tableHeader: true, children: encabezado.map((t, i) => celda(t, true, i)) }),
        ...filas.map((f) => new TableRow({ cantSplit: true, children: f.map((t, i) => celda(t, false, i)) }))],
    }));
    children.push(new Paragraph({ spacing: { after: 160 }, children: [] }));
  }

  // ---------- Portada ----------
  const C = (t, o = {}, sp = 200) => children.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: sp }, children: [run(t, o)] }));
  C('ESCUELA POLITÉCNICA NACIONAL', { bold: true, size: 28 }, 120);
  C('Facultad de Ingeniería de Sistemas', { size: 24 }, 900);
  C('APLICACIONES WEB AVANZADAS', { bold: true, size: 28 }, 200);
  C(`Laboratorio ${numero}`, { bold: true, size: 28 }, 500);
  C(titulo, { bold: true, size: 32 }, 200);
  if (subtitulo) C(subtitulo, { size: 24 }, 1000);
  C('Estudiante: Erick Alejandro Costa Aimacaña', { size: 24 });
  C('Grupo: GR2SW', { size: 24 });
  C(`Fecha: ${fecha}`, { size: 24 }, 400);
  repos.forEach((url) => children.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 120 },
    children: [run('Repositorio: ', { bold: true }), enlace(url)] })));
  SALTO();
  children.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 200 }, children: [run('ÍNDICE', { bold: true })] }));
  children.push(new TableOfContents('Índice', { hyperlink: true, headingStyleRange: '1-2' }));
  SALTO();

  // Sección común: uso de IA
  function USO_IA(filas, cierre) {
    H1('Uso de inteligencia artificial');
    P('El diseño del laboratorio es propio: se definieron la estructura de la solución y de las carpetas, el diseño de las pantallas, el esqueleto inicial del código, el modelo y las consultas en SQL Server y los casos de prueba. Sobre esa base se utilizó **Claude Code** (asistente de IA de Anthropic que trabaja en la terminal) como una herramienta más, siempre a partir de instrucciones concretas: completar y mejorar el código que ya tenía estructurado, autocompletar partes repetitivas, ayudar a diagnosticar errores, ejecutar y automatizar las pruebas diseñadas previamente y dar formato a este documento.');
    TABLA('Uso de la IA como herramienta de apoyo', ['Aspecto', 'Definición y decisiones propias', 'Uso de la IA'], filas, [18, 44, 38]);
    P('Cada cambio hecho con ayuda de la IA se revisó antes de aceptarlo y se verificó en la herramienta correspondiente (Visual Studio, SQL Server Management Studio, VS Code o el navegador). La IA no tomó decisiones de diseño por su cuenta: trabajó sobre indicaciones concretas, que se fueron ajustando a medida que avanzaba el laboratorio.');
    if (cierre) P(cierre);
  }

  function guardar(salidaDocx) {
    const doc = new Document({
      creator: 'Erick Alejandro Costa Aimacaña', title: `Laboratorio ${numero} - ${titulo}`,
      features: { updateFields: true },
      styles: {
        default: { document: { run: { font: TNR, size: 24 } } },
        paragraphStyles: [
          { id: 'Heading1', name: 'Heading 1', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: TNR, size: 26, bold: true, color: '000000' }, paragraph: { spacing: { before: 300, after: 120 }, keepNext: true } },
          { id: 'Heading2', name: 'Heading 2', basedOn: 'Normal', next: 'Normal', quickFormat: true, run: { font: TNR, size: 24, bold: true, color: '000000' }, paragraph: { spacing: { before: 200, after: 100 }, keepNext: true } },
        ],
      },
      sections: [{
        properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1418, bottom: 1418, left: 1418, right: 1418 } } },
        footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ font: TNR, size: 20, children: [PageNumber.CURRENT] })] })] }) },
        children,
      }],
    });
    fs.mkdirSync(require('path').dirname(salidaDocx), { recursive: true });
    return Packer.toBuffer(doc).then((b) => { fs.writeFileSync(salidaDocx, b); console.log(`Figuras: ${figura} · Tablas: ${tabla} -> ${salidaDocx}`); });
  }

  return { P, H1, H2, L, FIG, CODE, TABLA, SALTO, USO_IA, guardar };
}

module.exports = { nuevoInforme, RAIZ, CAPTURAS_USUARIO };
