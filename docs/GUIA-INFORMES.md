# Guía de trabajo: informes, capturas, código y entregas

> **Para un asistente nuevo (Claude u otro):** este archivo resume todo lo aprendido trabajando con Erick
> en sus asignaturas. Léelo completo antes de empezar. Las reglas de las secciones 1 a 6 aplican **a todas
> las asignaturas**; las secciones 9 y 10 son datos de contexto. Si algo de aquí contradice una instrucción
> nueva y explícita del usuario, manda la instrucción nueva (y conviene actualizar esta guía).

---

## 1. Cómo trabajar con el usuario

- **Idioma:** responder siempre en **español**. El usuario escribe rápido y con errores de tipeo
  (a veces dictado por voz): interpretar la intención, no corregirle.
- **Autonomía:** hacer **todo lo posible uno mismo** (compilar, probar, capturar, generar PDF, subir a GitHub)
  y pedir al usuario solo lo que de verdad requiere su intervención (iniciar sesión, permisos de administrador,
  acciones bloqueadas por el sistema). En ese caso, darle el comando exacto (en Claude Code: `! comando`).
- **Calidad:** "hazlo muy bien". Corregir problemas **en la causa raíz**, nunca tapando síntomas.
  Probar de punta a punta y **verificar visualmente con capturas** antes de dar algo por terminado.
- **Honestidad:** informar resultados reales (si una prueba falla, decirlo con el error). No inventar capturas
  ni resultados.
- **Mensajes cortos de avance** mientras se trabaja: el usuario quiere saber qué se está haciendo.
- **Nunca** usar datos o cuentas de **terceros** (familiares, compañeros) ni siquiera como dato de prueba o demo.
  Usar únicamente los datos del propio usuario.
- **Nunca copiar** el código o las soluciones de compañeros (aunque estén disponibles como referencia):
  el código debe ser original.
- No subir basura al repositorio (ver sección 5).

## 2. Redacción de informes (regla global)

- **Voz impersonal con "se"**: «se realizó», «se configuró», «se verificó», «se definió», «se ejecutó».
  - Prohibido: «el estudiante realizó», «el estudiante decidió», «realicé», «hicimos», «su equipo».
  - En tablas, encabezados sin sujeto: «Definición y decisiones propias», «Uso de la IA».
  - Excepción: la portada sí lleva «Estudiante: Erick Alejandro Costa Aimacaña».
  - Verificación final: buscar «estudiante» en el texto; solo debe aparecer en la portada.
  - Los verbos en pretérito sin «se» solo se aceptan si el sujeto es una herramienta (p. ej., «SSMS mostró»,
    «la IA completó»).
- **Describir antes de mostrar:** cada captura va precedida de un párrafo que explica qué se hizo y qué se ve.
  Después de la imagen, su pie: **«Figura N.** *descripción*».
- **Formato:** Times New Roman 12, texto justificado, interlineado 1,5, A4, márgenes de 2,5 cm,
  número de página en el pie, índice automático (TOC) tras la portada.
  - Títulos: Heading 1 (13 pt, negrita) y Heading 2 (12 pt, negrita), en negro.
  - Figuras y tablas numeradas («Figura N.», «Tabla N.»), pie en cursiva de 10 pt, centrado.
  - Código en bloques Consolas 9 pt con fondo gris claro (F3F3F3), con un título encima.
- **Portada (laboratorios AWA):** ESCUELA POLITÉCNICA NACIONAL · Facultad de Ingeniería de Sistemas ·
  APLICACIONES WEB AVANZADAS · Laboratorio N · título · subtítulo · Estudiante · Grupo: GR2SW · Fecha ·
  **Repositorio: enlace de GitHub**.
- **Estructura típica:** Objetivos → Requisitos/entorno → Desarrollo paso a paso (con capturas) →
  Pruebas (tabla de casos con resultado) → Diagramas → **Uso de inteligencia artificial** → Conclusiones →
  Recomendaciones (opcional) → Referencias.
- **Diagramas y gráficas:** se valoran. Arquitectura, modelo de datos, flujo, secuencia (cliente/servidor),
  capas N-Layer. Hechos como SVG propio y exportados a PNG a 2x (ver `herramientas/`).
- El enlace del repositorio va en la portada y, si aplica, en la sección de entrega.

## 3. Sección «Uso de inteligencia artificial» (obligatoria cuando se usó IA)

Marco correcto (el usuario lo exige así):

- **El diseño es propio:** se definieron la estructura de la solución y de las carpetas, el diseño de las
  pantallas, el esqueleto inicial del código, el modelo y las consultas SQL y los casos de prueba.
- **La IA fue una herramienta guiada**, "que necesita siempre alguien que le diga qué hacer": completó y
  mejoró código ya estructurado, autocompletó partes repetitivas, ayudó a diagnosticar errores, ejecutó y
  automatizó las pruebas diseñadas previamente y dio formato al documento.
- **Nunca** presentar a la IA como quien «propuso», «decidió», «diseñó» o «generó el sitio» por iniciativa propia.
- Verbos para la IA: completó, aplicó, autocompletó, ejecutó, automatizó, ayudó a diagnosticar, corrigió según
  indicación, dio formato.
- Tabla con columnas **Aspecto | Definición y decisiones propias | Uso de la IA** (anchos ~18/44/38 %).
- Párrafo de cierre: cada cambio se revisó antes de aceptarlo y se verificó en la herramienta correspondiente;
  la IA no tomó decisiones de diseño por su cuenta.
- Si hubo un error de la IA que se corrigió, contarlo con honestidad y en impersonal
  (p. ej., «en un primer momento interpretó "2.0" como un error y fue necesario corregirlo»).

## 4. Capturas de pantalla

- **Prioridad:** usar las capturas que el usuario deja en su carpeta (p. ej. `CAPTURAS/NN.png`); las que falten,
  tomarlas uno mismo. Revisar **cada** imagen antes de usarla (abrirla y mirarla).
- **Capturador** (este repo, `dist/Capturador.exe`): botón flotante + atajos
  `Ctrl+Alt+S` pantalla, `Ctrl+Alt+V` ventana activa, `Ctrl+Alt+R` región, `Ctrl+Alt+B` mostrar/ocultar.
  Guarda `NNN_fecha_hora_modo.png` y registra todo en `capturas.csv` (fecha, modo, proceso, título de la ventana),
  lo que permite saber después de qué es cada captura. También: `Capturador.exe --capturar pantalla|ventana`.
- **Herramientas PowerShell** (`herramientas/`):
  - `ventana.ps1` captura una ventana con PrintWindow aunque esté detrás (recorta el borde invisible de Win10).
  - `pantalla.ps1` captura toda la pantalla.
  - `unir.ps1` une capturas lado a lado; `hoja.ps1` arma hojas de contacto; `lado.ps1` pone Chrome y Brave lado a lado.
  - `teclas.ps1` / `clic.ps1` envían teclas o clics **solo si la ventana destino está en primer plano**.
- **Lecciones aprendidas (importante):**
  - Nunca enviar teclas "a ciegas": una vez terminaron escritas en la terminal del usuario. Verificar siempre
    la ventana en primer plano antes de cada tecla.
  - El usuario puede estar usando el equipo al mismo tiempo: avisar antes de tomar el control del mouse/teclado.
  - Las notificaciones de Windows 10 roban el foco (proceso `ShellExperienceHost`): no tomarlas como
    "ventana activa".
  - Brave tiende a maximizarse: capturar cada ventana por separado y unirlas con `unir.ps1`.
  - Para pruebas con dos usuarios/equipos: Chrome con un usuario y Brave con otro (p. ej., por IP de la red local).
  - No mostrar contraseñas ni datos de terceros en las capturas.
  - En Roblox Studio (MCP): `screen_capture` con `camera_position`/`look_at_position`; si se cambia algo para
    la foto (p. ej., `Lighting.TimeOfDay`), **restaurarlo** después.

## 5. Git, GitHub y entregables

- Cuenta de GitHub: **zeuspyEC** (`gh` ya autenticado). Autor de commits: `Erick Costa <erick.costa@epn.edu.ec>`.
- **Commits por etapas** lógicas (no un único commit gigante), mensajes descriptivos en español
  (p. ej., «Login y registro con bcrypt, JWT y validación de origen en el handshake»).
  **Commitear cada mejora/corrección** a medida que se hace.
- **Sin atribución de IA en los commits:** nada de `Co-Authored-By: Claude`, «Generated with…» ni similares.
- Etiquetas (`git tag vX.Y.Z`) y *releases* cuando hay una versión entregable; README en cada repo.
- **.gitignore** siempre: `bin/ obj/ packages/ node_modules/ .vs/ .env *.pem *.key *.db *.db-journal
  evidencias/ entregables/ *.zip *.pdf Thumbs.db` (y lo específico del proyecto).
  Nunca subir credenciales, certificados ni bases de datos.
- **Entregables** (carpeta `entregables/`, ignorada por git): `.docx` + `.pdf` del informe y `.zip` del código
  generado con `git archive` (solo archivos versionados).
- `gh repo create` puede ser bloqueado por el clasificador de permisos: si pasa, pedir al usuario que lo
  ejecute con `!` y luego hacer el push uno mismo. Verificar desde la carpeta correcta (en bash, las rutas de
  Windows con `\` se rompen: usar `/c/Users/...`).

## 6. Generación de informes (pipeline técnico)

1. **Generador Node** con el paquete `docx` (`herramientas/informes/base.cjs`):
   `nuevoInforme({numero, titulo, subtitulo, repos})` devuelve `P, H1, H2, L, FIG, CODE, TABLA, SALTO, USO_IA, guardar`.
   En el texto: `**negrita**`, `` `código` `` y `[[url]]` para enlaces. Un archivo `labNN.cjs` por informe.
2. **Word por COM** (`herramientas/informes/apdf.ps1`): abre el `.docx`, actualiza el índice y los campos,
   guarda y exporta a PDF.
3. **Revisión visual**: `vista.cjs` (pdf-to-png-converter) renderiza páginas a PNG para revisarlas una por una
   (figuras cortadas, tablas partidas, páginas en blanco).
4. **Diagramas**: SVG escrito a mano → PNG con puppeteer-core (Chrome instalado) a escala 2x.
5. Para correcciones masivas de redacción, scripts de «parche» que reemplazan texto exacto y **fallan si no
   encuentran el original** (así nada se cambia en silencio). Luego regenerar y volver a revisar.
6. **Documentos compartidos en línea** (Word Online de un grupo): no editar las secciones de otros. Preparar
   la parte propia en un `.docx` local con el mismo estilo y que el usuario la copie y pegue al final, bajo su
   nombre. (Automatizar el navegador del usuario es lento y choca con lo que él está haciendo.)

## 7. Lista de verificación antes de entregar

- [ ] Redacción impersonal (buscar «estudiante», «realicé», «hice»).
- [ ] Sección de IA con el marco correcto y su tabla.
- [ ] Cada figura con texto antes y pie «Figura N.»; numeración continua; capturas revisadas.
- [ ] Times New Roman 12, índice actualizado, números de página, portada con nombre completo, grupo y repo.
- [ ] PDF regenerado y revisado página por página.
- [ ] Repositorio limpio (sin bin/obj/node_modules/.env/claves/BD), commits por etapas, sin atribución de IA,
      push hecho y enlace verificado.
- [ ] ZIP con `git archive` si se pide.

## 8. Ideas en curso (KitInformes)

- Objetivo a futuro: que el Capturador **analice cada imagen** al capturarla (qué se ve, qué ventana, qué
  botones) para ayudar a **redactar** el informe automáticamente. Punto de extensión ya listo: en `config.ini`,
  `despues=comando {archivo}` se ejecuta tras cada captura.
- Ciclo **actuar y verificar** ya implementado: `Capturador.exe --sondear X Y` (hover: confirma que hay un
  elemento interactivo) y `--clic X Y` (confirma que la pantalla cambió). Comparan píxeles cerca del punto y en
  toda la pantalla, y dejan antes/después/marcado + `acciones.csv`. Lecciones: el cursor en reposo va en la
  esquina superior izquierda (la inferior derecha es «Mostrar escritorio»); los efectos de hover pueden ser de
  pocos píxeles (una sombra), por eso el umbral local es de 4 píxeles; relojes y temporizadores no cuentan.
- El análisis de imágenes con IA se haría con una **API** (p. ej., la API de Claude, que recibe la imagen y
  devuelve texto). Requiere una clave de API (de pago, por uso) guardada fuera del repositorio.
- Límite: la herramienta es para **documentar trabajo propio** (informes, evidencias), no para responder
  automáticamente cuestionarios o evaluaciones.

## 9. Datos del usuario

- **Nombre completo:** Erick Alejandro Costa Aimacaña (en portadas). Formato corto de archivos de examen:
  `Costa.Erick.Ex1` (Apellido.Nombre).
- **Universidad:** Escuela Politécnica Nacional (EPN), Facultad de Ingeniería de Sistemas.
- **Aplicaciones Web Avanzadas:** grupo **GR2SW**.
- **Desarrollo de Juegos Interactivos:** trabajo grupal en Roblox Studio («Dinero inteligente»), cada integrante
  con su carpeta en Workspace (la suya: `Workspace/Erick`) e informe grupal en Word Online.
- Usuario de prueba propio con formato Nombre.Apellido: `Erick.Costa` / `erick.costa`.

## 10. Notas técnicas del entorno (Windows 10)

- **SQL Server 2022** instancia por defecto, modo mixto, TCP/IP en 1433. En este servidor en español los scripts
  necesitan `SET DATEFORMAT ymd`. SQLOLEDB requiere TCP/IP habilitado.
- **.NET Framework:** usar **exactamente** la versión que pide cada guía (2.0, 4.8…). Con 2.0 en IIS Express:
  pool `Clr2IntegratedAppPool`; sin `<%: %>`, LINQ, `TextMode="Number"` ni `targetFramework`.
- Visual Studio 2026 necesita el componente «Plantillas de proyecto y de elemento de .NET Framework» para crear
  «Aplicación web ASP.NET (.NET Framework)».
- Rutas largas (> 260 caracteres) rompen MSBuild con paquetes NuGet: normalizar `HintPath` con
  `$([System.IO.Path]::GetFullPath(...))`.
- `Set-ExecutionPolicy` y acciones de administrador: las ejecuta el usuario.
- `Add-Type` en PowerShell a veces falla con «Acceso denegado» al compilar en `%TEMP%` (antivirus): reintentar
  o compilar un `.exe` pequeño con `csc.exe` de .NET Framework (siempre disponible en Windows).
- Python no está instalado; Node.js sí (v26). Para editar archivos, preferir las herramientas del asistente o Node.
