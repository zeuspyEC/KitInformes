// Ejemplo mínimo de informe con el generador: node ejemplo.cjs  ->  salida/ejemplo.docx
// Luego: powershell -File apdf.ps1 -docx <ruta absoluta al .docx>  (actualiza el índice y exporta a PDF)
const path = require('path');
const { nuevoInforme } = require('./base.cjs');

const { P, H1, H2, L, CODE, TABLA, USO_IA, guardar } = nuevoInforme({
  numero: '00',
  titulo: 'Título del laboratorio',
  subtitulo: 'Subtítulo o tema',
  repos: ['https://github.com/zeuspyEC/Repositorio'],
});

H1('1. Objetivos');
L('Objetivo 1 redactado en infinitivo.');
H1('2. Desarrollo');
H2('2.1 Configuración del entorno');
P('Se configuró el entorno con **la herramienta X** y se verificó con `comando --version`.');
// FIG('C:/ruta/captura.png', 'Descripción de lo que se ve.');  // siempre después del párrafo que la explica
CODE('Archivo de ejemplo', 'console.log("hola");');
H1('3. Pruebas');
TABLA('Casos de prueba', ['Caso', 'Resultado esperado', 'Resultado'], [['Login correcto', 'Ingresa al sistema', 'Correcto']], [30, 45, 25]);
USO_IA([
  ['Estructura', 'Se definieron las carpetas, las capas y el esqueleto del código.', 'Completó las clases sobre esa estructura.'],
  ['Pruebas e informe', 'Se definieron los casos de prueba y qué evidenciar.', 'Ejecutó los casos y dio formato al documento.'],
]);
H1('Conclusiones');
L('Conclusión redactada en impersonal.');

guardar(path.join(__dirname, 'salida', 'ejemplo.docx'));
