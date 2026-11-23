# KitInformes

Herramienta para documentar trabajos académicos: tomar capturas sin interrumpir el trabajo, **entender lo que
se ve en pantalla** (sin IA), interactuar con las aplicaciones (clic por nombre, escribir, verificar) y dejar
cada paso redactado para el informe.

## KitInformes.exe (portable)

Un único archivo `dist\KitInformes.exe` (~94 KB). No se instala ni necesita nada más: usa .NET Framework 4,
que ya viene con Windows 10/11. Se puede copiar a cualquier carpeta o memoria USB; guarda su configuración
(`config.ini`) y sus capturas (`Capturas\`) junto al ejecutable.

Al abrirlo aparece un **botón flotante** con una cámara, siempre visible, que no quita el foco a la ventana
en la que se trabaja. Se arrastra a cualquier lugar y recuerda su posición.

| Acción | Botón / atajo |
|---|---|
| Capturar la pantalla completa | Clic en el botón · `Ctrl+Alt+S` |
| Capturar solo la ventana activa | `Ctrl+Alt+V` |
| Capturar una región (arrastrar; `Esc` cancela) | `Ctrl+Alt+R` |
| Mostrar u ocultar el botón (los atajos siguen funcionando) | `Ctrl+Alt+B` |
| **Configuración** | Clic en el ícono de la bandeja · clic derecho en el botón → *Configuración…* |

### Configuración (dos opciones)

1. **Botón flotante:** mostrarlo u ocultarlo.
2. **Modo de uso:**
   - **Solo capturar:** guarda la imagen y la anota en `capturas.csv`.
   - **Capturar y analizar:** además describe lo que se ve —ventana, programa, textos visibles, botones,
     campos y enlaces— en `NNN_…_analisis.md` junto a la imagen y muestra un resumen como respuesta, p. ej.:
     *«Se observa la ventana «Canciones - PoliMusic V3» (chrome). Contiene 12 botones, 2 campos, 9 enlaces.
     Textos principales: «Catálogo», «Canciones», …»*.

### Pantalla limpia

Antes de cada captura (y de cada acción de un guion) se cierran solas las ventanas emergentes que estorban,
como **«¿Guardar la contraseña?»**, pulsando **«Nunca»**, **«No, gracias»**, **«Ahora no»**… Se hace con la
acción propia del botón (accesibilidad de Windows), sin mover el mouse. La lista está en `config.ini`
(`descartar=`).

### Cursor en tiempo real

Cuando el programa actúa, el cursor **se desliza** hasta el destino y cada clic se marca con un **anillo rojo**
(que deja pasar el clic y se quita antes de la captura de verificación). Se desactiva con `cursor_visible=0`.

### Comandos de terminal

```
KitInformes --capturar pantalla|ventana            una captura y termina
KitInformes analizar [--ventana "título"]          captura + contexto de lo que se ve
KitInformes limpiar [--ventana "título"]           cierra ventanas emergentes
KitInformes elementos [--ventana "título"]         botones, enlaces y campos con nombre y posición
KitInformes clic-nombre "Entrar" [--tipo botón] [--ventana "título"]   busca, hace clic y verifica
KitInformes --sondear X Y | --clic X Y             ¿reacciona al hover? / clic verificado (antes/después)
KitInformes explorar X Y ANCHO ALTO [--paso 24]    zonas interactivas por hover (solo visual)
KitInformes guion pasos.txt                        ejecuta un guion y deja registro.md
KitInformes --configuracion | --salir
```

El **clic verificado** compara capturas antes/después: cuenta como efectivo solo si cambió el lugar donde se
hizo clic o casi toda la pantalla (una ventana emergente en otra zona no cuenta). Guarda `-antes`, `-despues`
y `-marcado.png` y una fila en `acciones.csv`.

### Guiones

Un guion es un archivo de texto con un paso por línea:

```
abrir "C:\Program Files\Google\Chrome\Application\chrome.exe" --force-renderer-accessibility http://localhost:53403/Login
ventana Iniciar sesión - PoliMusic V3
clic-nombre Usuario | campo
tecla CTRL+A
escribir Erick.Costa
clic-nombre Contraseña | campo
escribir-oculto ********
clic-nombre Entrar | botón
verificar-titulo Inicio - PoliMusic V3
clic-nombre Canciones | enlace
clic-nombre Reproducir Adventure | botón
verificar Pausar Adventure | botón
capturar Se capturó la canción en reproducción
```

Instrucciones: `abrir`, `ventana`, `maximizar`, `esperar`, `capturar`, `analizar`, `limpiar`, `elementos`,
`clic-nombre`, `clic-si-existe`, `clic`, `escribir`, `escribir-oculto`, `tecla` (`ENTER`, `CTRL+A`…),
`verificar`, `verificar-titulo`, `cerrar`. Cada paso se verifica y queda en `registro.md` **en voz impersonal**
(«Se hizo clic en el botón «Entrar» y la pantalla cambió»), listo para el informe. Ejemplo real:
[`pruebas/guiones/polimusic-v3.txt`](pruebas/guiones/polimusic-v3.txt) (25 pasos sobre el laboratorio 04).

> Chrome/Edge/Brave exponen el contenido de la página a la accesibilidad de Windows si se abren con
> `--force-renderer-accessibility` (o cuando hay un lector de pantalla activo).

### Alcance sin IA

- **Hace:** encontrar botones, campos y enlaces **que tienen nombre**; leer textos visibles; hacer clic,
  escribir y verificar que la pantalla cambió; cerrar ventanas emergentes; recorrer flujos completos con un
  guion y documentarlos.
- **No hace:** entender el significado de una imagen sin nombres (gráficos, juegos, imágenes) ni decidir por sí
  solo qué hacer después: eso lo define quien escribe el guion (o, a futuro, un módulo de IA conectado con
  `despues=` en `config.ini`).

## Compilar y probar

```powershell
powershell -ExecutionPolicy Bypass -File construir.ps1                 # -> dist\KitInformes.exe
powershell -ExecutionPolicy Bypass -File pruebas\ejecutar-pruebas.ps1  # 21 pruebas de punta a punta
```

Las pruebas usan una copia aislada en `pruebas\tmp` y **solo ventanas de prueba propias** (nunca programas del
usuario). Toman el control del teclado y el mouse alrededor de un minuto: no usar el equipo mientras corren.

**Antivirus:** un programa que captura la pantalla, lee otras ventanas y mueve el mouse se parece, por su
comportamiento, a software de control remoto, y algunos antivirus (p. ej., Bitdefender) lo bloquean por
heurística. Se resuelve agregando la carpeta del proyecto a las excepciones del antivirus.

## Generador de informes

`herramientas/informes/`: generador en Node.js (`docx`) con portada, índice, figuras y tablas numeradas,
bloques de código y la sección de uso de IA; `apdf.ps1` actualiza el índice y exporta a PDF con Word;
`vista.cjs` convierte el PDF a imágenes; `diagramas.cjs` dibuja diagramas SVG → PNG. Ver `ejemplo.cjs`.

## Guía

[`docs/GUIA-INFORMES.md`](docs/GUIA-INFORMES.md) reúne las reglas de redacción, formato, capturas, uso de IA,
Git y entregas. Está pensada para que un asistente que empieza de cero la lea y trabaje igual.
