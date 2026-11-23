# KitInformes

Herramientas para documentar trabajos académicos: tomar capturas sin interrumpir el trabajo,
registrar de qué es cada una y generar informes Word/PDF con un formato uniforme.

## Capturador (portable)

Un único `Capturador.exe` (sin instalación, requiere solo .NET Framework 4, incluido en Windows 10/11).
Al abrirlo aparece un **botón flotante** con una cámara, siempre visible y que no quita el foco a la ventana
en la que se está trabajando. Se puede arrastrar a cualquier lugar y recuerda su posición.

| Acción | Botón / atajo |
|---|---|
| Capturar la pantalla completa | Clic en el botón · `Ctrl+Alt+S` |
| Capturar solo la ventana activa | `Ctrl+Alt+V` |
| Capturar una región (arrastrar con el mouse; `Esc` cancela) | `Ctrl+Alt+R` |
| Mostrar u ocultar el botón (los atajos siguen funcionando) | `Ctrl+Alt+B` |
| Más opciones (carpeta, registro, portapapeles, sonido, salir) | Clic derecho en el botón o en el ícono de la bandeja |

Cada captura se guarda en `Capturas/` (junto al .exe, configurable) como
`001_2026-11-23_10-00-15_pantalla.png`, `002_…_ventana-chrome.png`, `003_…_region.png`, se copia al
portapapeles y se registra en **`capturas.csv`**:

```
numero;fecha;hora;modo;archivo;ancho;alto;proceso;titulo_ventana
003;2026-11-23;10:00:24;ventana;003_2026-11-23_10-00-24_ventana-notepad.png;626;413;"notepad";"Sin título: Bloc de notas"
```

Así, al redactar el informe se sabe de qué programa y ventana es cada imagen.

**Línea de comandos:**

```
Capturador.exe --capturar pantalla     # una captura y termina
Capturador.exe --capturar ventana
Capturador.exe --salir                 # cierra la instancia abierta
Capturador.exe --probar-atajos         # código de salida = atajos ocupados por otras aplicaciones
```

**Extensión:** en `config.ini`, `despues=` define un comando que se ejecuta tras cada captura
(`{archivo}` se reemplaza por la ruta del PNG). Es el punto donde se conectará el análisis automático de
imágenes con IA.

### Compilar y probar

```powershell
powershell -ExecutionPolicy Bypass -File capturador\compilar.ps1   # -> dist\Capturador.exe
powershell -ExecutionPolicy Bypass -File pruebas\probar.ps1        # 13 pruebas de punta a punta
```

Las pruebas usan una copia aislada en `%TEMP%` y toman el control del teclado y el mouse unos segundos
(atajos, clic en el botón, selección de región, ventana del Bloc de notas): no usar el equipo mientras corren.

## Generador de informes

`herramientas/informes/`: generador en Node.js (`docx`) con portada, índice, figuras y tablas numeradas,
bloques de código y la sección de uso de IA; `apdf.ps1` actualiza el índice y exporta a PDF con Word;
`vista.cjs` convierte el PDF a imágenes para revisarlo; `diagramas.cjs` dibuja diagramas en SVG y los
exporta a PNG. Ver `ejemplo.cjs`.

```
cd herramientas\informes
npm install
node ejemplo.cjs
powershell -File apdf.ps1 -docx "%CD%\salida\ejemplo.docx"
```

`herramientas/*.ps1`: capturas de una ventana (aunque esté detrás), de la pantalla, unión de imágenes,
hojas de contacto y envío seguro de teclas/clics (solo si la ventana está en primer plano).

## Guía

[`docs/GUIA-INFORMES.md`](docs/GUIA-INFORMES.md) reúne las reglas de redacción, formato, capturas, uso de IA,
Git y entregas. Está pensada para que un asistente que empieza de cero la lea y trabaje igual.
