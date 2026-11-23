// Genera los diagramas y gráficas de los informes como PNG (SVG renderizado con Chrome, escala 2x)
const fs = require('fs');
const path = require('path');
const puppeteer = require('puppeteer-core');
const OUT = path.join(__dirname, 'diagramas');
fs.mkdirSync(OUT, { recursive: true });

// ---------- piezas SVG ----------
const C = { tinta: '#1f2937', suave: '#5b6475', borde: '#9aa3b2', fondo: '#f5f7fa', azul: '#2f5d8a', azulF: '#e6eef7',
  verde: '#2e7d5b', verdeF: '#e5f3ec', ambar: '#b7791f', ambarF: '#fbf1df', rojo: '#b42318', rojoF: '#fdecea', gris: '#eef0f3' };
const esc = (t) => String(t).replace(/&/g, '&amp;').replace(/</g, '&lt;');
const defs = `<defs>
  <marker id="f" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="${C.tinta}"/></marker>
  <marker id="fr" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="${C.rojo}"/></marker>
  <marker id="fv" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="${C.verde}"/></marker>
</defs>`;
function caja(x, y, w, h, titulo, lineas = [], o = {}) {
  const fill = o.fill || C.fondo, stroke = o.stroke || C.borde;
  let s = `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="10" fill="${fill}" stroke="${stroke}" stroke-width="${o.sw || 1.5}" ${o.dash ? 'stroke-dasharray="6 5"' : ''}/>`;
  const ty = lineas.length ? y + 28 : y + h / 2 + 6;
  s += `<text x="${x + w / 2}" y="${ty}" text-anchor="middle" font-weight="700" font-size="${o.fs || 17}" fill="${o.color || C.tinta}">${esc(titulo)}</text>`;
  lineas.forEach((l, i) => { s += `<text x="${x + w / 2}" y="${y + 52 + i * 21}" text-anchor="middle" font-size="14" fill="${C.suave}">${esc(l)}</text>`; });
  return s;
}
function flecha(x1, y1, x2, y2, etiqueta, o = {}) {
  const color = o.color || C.tinta, m = o.color === C.rojo ? 'fr' : o.color === C.verde ? 'fv' : 'f';
  let s = `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${color}" stroke-width="${o.sw || 1.8}" ${o.dash ? 'stroke-dasharray="7 5"' : ''} marker-end="url(#${m})" ${o.doble ? `marker-start="url(#${m})"` : ''}/>`;
  if (etiqueta) {
    const lx = o.lx ?? (x1 + x2) / 2, ly = o.ly ?? (y1 + y2) / 2 - 8;
    [].concat(etiqueta).forEach((t, i) => { s += `<text x="${lx}" y="${ly + i * 17}" text-anchor="${o.anchor || 'middle'}" font-size="13.5" fill="${o.tcolor || color}" ${o.bold ? 'font-weight="700"' : ''}>${esc(t)}</text>`; });
  }
  return s;
}
const texto = (x, y, t, o = {}) => `<text x="${x}" y="${y}" text-anchor="${o.anchor || 'middle'}" font-size="${o.fs || 14}" fill="${o.color || C.suave}" ${o.bold ? 'font-weight="700"' : ''} ${o.italic ? 'font-style="italic"' : ''}>${esc(t)}</text>`;
const svg = (w, h, cuerpo) => `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" font-family="Segoe UI, Arial, sans-serif">${defs}<rect width="${w}" height="${h}" fill="#fff"/>${cuerpo}</svg>`;

// Diagrama de secuencia: actores con líneas de vida y mensajes numerados
function secuencia(w, actores, mensajes, alto) {
  let s = '';
  const xs = actores.map((a) => a.x);
  actores.forEach((a) => {
    s += caja(a.x - a.w / 2, 16, a.w, 54, a.t, [], { fill: a.fill || C.azulF, stroke: a.stroke || C.azul, fs: 15 });
    if (a.sub) s += texto(a.x, 88, a.sub, { fs: 12.5 });
    s += `<line x1="${a.x}" y1="92" x2="${a.x}" y2="${alto - 16}" stroke="${C.borde}" stroke-dasharray="4 5"/>`;
  });
  mensajes.forEach((m) => {
    if (m.nota) { s += `<rect x="${m.x}" y="${m.y - 18}" width="${m.w}" height="${m.h || 28}" rx="6" fill="${m.fill || C.ambarF}" stroke="${m.stroke || C.ambar}"/>` + texto(m.x + m.w / 2, m.y + 1, m.nota, { fs: 13, color: C.tinta }); return; }
    const x1 = xs[m.de], x2 = xs[m.a];
    s += flecha(x1 + (x2 > x1 ? 4 : -4), m.y, x2 + (x2 > x1 ? -6 : 6), m.y, m.t, { color: m.color, dash: m.dash, lx: (x1 + x2) / 2, ly: m.y - 7, bold: m.bold });
  });
  return svg(w, alto, s);
}

const D = {};

// ===== Lab 01: arquitectura de los tres proyectos =====
D['lab01-arquitectura'] = svg(1000, 470, [
  texto(500, 32, 'Solución TestAppDatabase: tres tipos de proyecto, una sola base de datos', { fs: 18, bold: true, color: C.tinta }),
  caja(30, 70, 280, 92, 'ConsoleApp', ['Aplicación de consola · .NET 10', 'Microsoft.Data.SqlClient'], { fill: C.azulF, stroke: C.azul }),
  caja(30, 190, 280, 92, 'DesktopApp', ['Windows Forms · .NET 10', 'DataGridView'], { fill: C.azulF, stroke: C.azul }),
  caja(30, 310, 280, 112, 'WebApp', ['ASP.NET Web Forms · .NET Fw 4.8', 'GridView · IIS Express :51481', 'se abre en el navegador'], { fill: C.azulF, stroke: C.azul }),
  caja(660, 150, 310, 190, 'SQL Server 2022', ['instancia MSSQLSERVER', 'TCP/IP habilitado · puerto 1433', '', 'BDD_PoliMusic', 'SELECT * FROM TBL_SONG'], { fill: C.verdeF, stroke: C.verde }),
  flecha(312, 116, 656, 190, ['TCP localhost,1433', 'login usr_polimusic'], { lx: 470, ly: 128 }),
  flecha(312, 236, 656, 245, ['Integrated Security', '(usuario de Windows)'], { lx: 480, ly: 222 }),
  flecha(312, 366, 656, 300, ['SQL Server auth', 'login usr_polimusic'], { lx: 480, ly: 352 }),
  caja(660, 380, 310, 62, 'SQL Server Profiler', ['RPC:Completed · SQL:BatchStarting'], { fill: C.ambarF, stroke: C.ambar, dash: true, fs: 15 }),
  flecha(815, 378, 815, 344, 'observa', { dash: true, color: C.ambar, lx: 850, ly: 366 }),
].join(''));

// ===== Lab 01: modelo de datos con la tabla núcleo =====
(() => {
  const t = (x, y, n, cols, o = {}) => caja(x, y, 200, 44 + cols.length * 21, n, cols, o);
  D['lab01-modelo'] = svg(1000, 560, [
    texto(500, 24, 'BDD_PoliMusic: TBL_SONG es la tabla núcleo (recibe tres relaciones)', { fs: 18, bold: true, color: C.tinta }),
    t(400, 210, 'TBL_SONG', ['PK ID_SONG', 'SONG_NAME', 'SONG_PATH', 'PLAYS'], { fill: C.ambarF, stroke: C.ambar, sw: 3 }),
    t(400, 60, 'TBL_PLAYLIST_SONG', ['FK ID_PLAYLIST', 'FK ID_SONG']),
    t(60, 230, 'TBL_TRACKLIST', ['FK ID_ALBUM', 'FK ID_SONG']),
    t(740, 230, 'TBL_PLAYER', ['FK ID_SONG', 'FK ID_ARTIST']),
    t(60, 60, 'TBL_PLAYLIST', ['PK ID_PLAYLIST', 'FK ID_USER', 'TITLE · TYPE']),
    t(60, 420, 'TBL_ALBUM', ['PK ID_ALBUM', 'TITLE_ALBUM']),
    t(740, 420, 'TBL_ARTIST', ['PK ID_ARTIST', 'ARTIST_NAME']),
    t(740, 60, 'TBL_USER', ['PK ID_USER', 'USERNAME · PASSWORD', 'USER_TYPE']),
    t(400, 430, 'TBL_CATALOG', ['PK ID_CATALOG', 'catálogo de tipos'], { dash: true }),
    flecha(500, 148, 500, 206, '', { color: C.ambar, sw: 2.5 }), flecha(262, 262, 396, 255, '', { color: C.ambar, sw: 2.5 }), flecha(738, 262, 604, 255, '', { color: C.ambar, sw: 2.5 }),
    flecha(398, 85, 262, 85, ''), flecha(160, 290, 160, 416, ''), flecha(840, 290, 840, 416, ''), `<path d="M 160 58 L 160 42 L 840 42 L 840 54" fill="none" stroke="${C.tinta}" stroke-width="1.8" marker-end="url(#f)"/>`,
    texto(500, 540, 'Las flechas van de la tabla con la clave foránea a la tabla referenciada. TBL_CATALOG no tiene claves foráneas.', { fs: 13 }),
  ].join(''));
})();

// ===== Lab 02: flujo de una petición en ASP clásico =====
D['lab02-flujo'] = svg(1000, 400, [
  texto(500, 32, 'PoliMusic V1: recorrido de una petición en ASP clásico', { fs: 18, bold: true, color: C.tinta }),
  caja(20, 120, 170, 110, 'Navegador', ['HTML + CSS + JS', '<audio controls>'], { fill: C.azulF, stroke: C.azul }),
  caja(250, 90, 260, 170, 'IIS Express :51482', ['módulo ASP (asp.dll)', 'login.asp · canciones.asp', 'crud_cancion.asp · logout.asp', 'Session("usuario"), ("rol")'], { fill: C.ambarF, stroke: C.ambar }),
  caja(570, 120, 180, 110, 'ADODB', ['proveedor SQLOLEDB', 'comandos con parámetros'], { fill: C.gris }),
  caja(800, 110, 180, 130, 'SQL Server', ['localhost,1433', 'BDD_PoliMusic', 'TBL_USER · TBL_SONG'], { fill: C.verdeF, stroke: C.verde }),
  flecha(192, 160, 246, 160, 'petición'), flecha(246, 200, 192, 200, 'HTML', { ly: 218 }),
  flecha(512, 175, 566, 175, ''), flecha(752, 175, 796, 175, 'TCP/IP', { ly: 165 }),
  caja(250, 300, 500, 70, 'Control de roles', ['USER_TYPE 0 → solo escucha · USER_TYPE 1 → CRUD (validado en el servidor)'], { fill: C.fondo, dash: true, fs: 15 }),
  flecha(380, 262, 380, 296, '', { dash: true }),
].join(''));

// ===== Lab 03: monolito =====
D['lab03-monolito'] = svg(1000, 430, [
  texto(500, 32, 'PoliMusic V2: un solo proyecto y un solo ensamblado (.NET Framework 2.0)', { fs: 18, bold: true, color: C.tinta }),
  `<rect x="40" y="60" width="640" height="340" rx="14" fill="#fff" stroke="${C.azul}" stroke-width="2.5"/>`,
  texto(360, 88, 'WebAppPoliMusicV2  →  WebAppPoliMusicV2.dll', { fs: 16, bold: true, color: C.azul }),
  caja(70, 110, 580, 70, 'Presentación', ['Login · SignUp · Logout · Song.aspx · Play.ashx · FileManagement'], { fill: C.azulF, stroke: C.azul, fs: 15 }),
  caja(70, 200, 280, 70, 'BLL/', ['validaciones de negocio'], { fill: C.gris, fs: 15 }),
  caja(370, 200, 280, 70, 'DAL/', ['ADO.NET con parámetros'], { fill: C.gris, fs: 15 }),
  caja(70, 290, 580, 70, 'Entities/ · Util/', ['User · Song · Constants · Hash SHA1'], { fill: C.gris, fs: 15 }),
  flecha(210, 182, 210, 196, ''), flecha(352, 235, 366, 235, ''),
  caja(740, 100, 230, 110, 'IIS Express :51483', ['grupo de aplicaciones', 'Clr2IntegratedAppPool', '(CLR 2.0)'], { fill: C.ambarF, stroke: C.ambar, fs: 15 }),
  caja(740, 260, 230, 100, 'SQL Server', ['BDD_PoliMusic', 'login usr_polimusic'], { fill: C.verdeF, stroke: C.verde, fs: 15 }),
  flecha(682, 155, 736, 155, 'ejecuta', { ly: 145 }), flecha(652, 235, 736, 300, '', {}),
  texto(360, 418, 'Las carpetas ordenan el código, pero todo se compila y despliega junto: un cambio obliga a recompilar el proyecto completo.', { fs: 13 }),
].join(''));

// ===== Lab 04: capas y dependencias =====
D['lab04-capas'] = svg(1000, 450, [
  texto(500, 32, 'PoliMusic V3: arquitectura N-Layer y dirección de las referencias', { fs: 18, bold: true, color: C.tinta }),
  caja(330, 60, 340, 92, 'WebAppPoliMusicV3', ['Presentación · Web Forms .NET Fw 4.8', 'Site.Master · Song · Login · Play.ashx'], { fill: C.azulF, stroke: C.azul }),
  caja(330, 200, 340, 80, 'BLL.PoliMusicV3', ['Lógica de negocio · validaciones · hash'], { fill: C.verdeF, stroke: C.verde }),
  caja(330, 330, 340, 80, 'DAL.PoliMusicV3', ['Acceso a datos · SqlClient parametrizado'], { fill: C.ambarF, stroke: C.ambar }),
  caja(40, 200, 220, 210, 'EntityLayer', ['User · Song', 'Util/Constants', 'Util/Hash', '', 'la usan todas', 'las capas'], { fill: C.gris }),
  caja(760, 330, 210, 80, 'SQL Server', ['BDD_PoliMusic'], { fill: C.fondo }),
  flecha(500, 154, 500, 196, 'referencia', { lx: 555, ly: 180 }),
  flecha(500, 282, 500, 326, 'referencia', { lx: 555, ly: 309 }),
  flecha(672, 370, 756, 370, ''),
  flecha(328, 110, 262, 230, '', {}), flecha(328, 240, 262, 270, ''), flecha(328, 370, 262, 360, ''),
  `<path d="M 680 110 C 820 110, 820 330, 680 360" fill="none" stroke="${C.rojo}" stroke-width="2" stroke-dasharray="7 5" marker-end="url(#fr)"/>`,
  texto(895, 220, 'PROHIBIDO', { fs: 14, bold: true, color: C.rojo }),
  texto(895, 238, 'Web → DAL rompería', { fs: 13, color: C.rojo }), texto(895, 255, 'la arquitectura', { fs: 13, color: C.rojo }),
  texto(500, 438, 'Cada capa solo conoce a la inmediatamente inferior; la presentación nunca accede directamente a la base de datos.', { fs: 13 }),
].join(''));

// ===== Lab 04: gráfica de reproducciones por canción (datos reales de TBL_SONG) =====
(() => {
  const datos = JSON.parse(process.env.PLAYS || '[]');
  const max = Math.max(...datos.map((d) => d.v), 1), x0 = 180, ancho = 680, top = 70, fila = 52;
  let s = texto(500, 32, 'Reproducciones por canción (TBL_SONG.PLAYS, tras las pruebas)', { fs: 18, bold: true, color: C.tinta });
  for (let k = 0; k <= max; k++) {
    const x = x0 + (ancho * k) / max;
    s += `<line x1="${x}" y1="${top - 6}" x2="${x}" y2="${top + datos.length * fila}" stroke="#e5e7eb"/>` + texto(x, top + datos.length * fila + 22, k, { fs: 12.5 });
  }
  datos.forEach((d, i) => {
    const y = top + i * fila + 10, w = (ancho * d.v) / max;
    s += texto(x0 - 14, y + 21, d.n, { anchor: 'end', fs: 15, color: C.tinta });
    s += `<rect x="${x0}" y="${y}" width="${Math.max(w, 2)}" height="30" rx="4" fill="${C.azul}"/>`;
    s += texto(x0 + w + 10, y + 21, d.v, { anchor: 'start', fs: 15, bold: true, color: C.tinta });
  });
  s += texto(x0 + ancho / 2, top + datos.length * fila + 46, 'número de reproducciones (cada inicio de canción suma 1 mediante Play.ashx)', { fs: 13 });
  D['lab04-plays'] = svg(1000, top + datos.length * fila + 62, s);
})();

// ===== Lab 05: secuencia WebSocket =====
D['lab05-secuencia'] = secuencia(1000, [
  { x: 150, w: 210, t: 'Navegador A', sub: 'Erick.Costa · Chrome' },
  { x: 500, w: 230, t: 'Servidor Node.js', sub: 'Express + ws · puerto 3000', fill: C.verdeF, stroke: C.verde },
  { x: 850, w: 210, t: 'Navegador B', sub: 'Ana.Prueba · Brave' },
], [
  { de: 0, a: 1, y: 130, t: '1. POST /api/login → JWT (bcrypt + SQLite)' },
  { de: 0, a: 1, y: 175, t: '2. GET /?token=…  Upgrade: websocket' },
  { nota: 'verifyClient: Origin permitido + JWT válido', x: 360, w: 280, y: 210 },
  { de: 1, a: 0, y: 250, t: '3. 101 Switching Protocols', color: C.verde },
  { de: 1, a: 0, y: 290, t: '4. {type:"history"} historial', dash: true },
  { de: 1, a: 2, y: 290, t: '{type:"system"} «se unió» · {type:"users"}', dash: true },
  { de: 0, a: 1, y: 340, t: '5. {type:"typing"}' },
  { de: 1, a: 2, y: 340, t: '«escribiendo…»', dash: true },
  { de: 0, a: 1, y: 390, t: '6. {type:"message", text}' },
  { nota: 'valida 1–300 caracteres y guarda en SQLite', x: 360, w: 280, y: 425 },
  { de: 1, a: 0, y: 465, t: 'difusión (self: true → derecha)', dash: true },
  { de: 1, a: 2, y: 465, t: 'difusión (self: false → izquierda)', dash: true },
  { de: 2, a: 1, y: 515, t: '7. cierre de la conexión' },
  { de: 1, a: 0, y: 555, t: '{type:"system"} «salió del chat»', dash: true },
], 590);

// ===== Lab 06: autenticación JWT y WSS =====
D['lab06-secuencia'] = secuencia(1000, [
  { x: 160, w: 220, t: 'Navegador', sub: 'frontend/script.js' },
  { x: 520, w: 250, t: 'Backend HTTPS :5000', sub: 'Express + cert.pem / key.pem', fill: C.verdeF, stroke: C.verde },
  { x: 860, w: 200, t: 'Servidor WSS /ws', sub: 'ws + verifyClient', fill: C.ambarF, stroke: C.ambar },
], [
  { nota: 'Todo el tráfico va cifrado con TLS (certificado autofirmado, CN=localhost)', x: 230, w: 540, y: 125 },
  { de: 0, a: 1, y: 175, t: '1. POST /auth {username, password}' },
  { de: 1, a: 0, y: 215, t: '2. 200 {token: JWT firmado, expira en 15 min}', color: C.verde },
  { de: 1, a: 0, y: 250, t: '   401 «Credenciales incorrectas»', color: C.rojo, dash: true },
  { de: 0, a: 1, y: 295, t: '3. GET /userInfo  Authorization: Bearer <JWT>' },
  { de: 0, a: 2, y: 345, t: '4. wss://localhost:5000/ws?token=<JWT>', bold: true },
  { nota: 'jwt.verify(token, JWT_SECRET) en el handshake', x: 700, w: 290, y: 385 },
  { de: 2, a: 0, y: 430, t: '5a. token válido → 101 Switching Protocols', color: C.verde },
  { de: 2, a: 0, y: 465, t: '5b. token inválido o vencido → 401, el socket no se abre', color: C.rojo, dash: true },
  { de: 0, a: 2, y: 515, t: '6. mensajes cifrados por wss → difusión a los clientes' },
], 550);

(async () => {
  const b = await puppeteer.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe' });
  const p = await b.newPage();
  for (const [n, s] of Object.entries(D)) {
    const w = +s.match(/width="(\d+)"/)[1], h = +s.match(/height="(\d+)"/)[1];
    await p.setViewport({ width: w, height: h, deviceScaleFactor: 2 });
    await p.setContent(`<html><body style="margin:0">${s}</body></html>`);
    await p.screenshot({ path: path.join(OUT, n + '.png'), clip: { x: 0, y: 0, width: w, height: h } });
    console.log('ok', n);
  }
  await b.close();
})();
