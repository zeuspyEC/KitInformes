// Análisis de lo que se ve (sin IA) y limpieza de ventanas emergentes antes de capturar.
// Ambos usan UI Automation (la accesibilidad de Windows): nombres y posiciones de textos, botones y campos.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace KitInformes
{
    static class Limpieza
    {
        /// <summary>
        /// Busca, en todas las ventanas del mismo programa que «hwnd» (las burbujas del navegador son ventanas aparte),
        /// botones cuyo nombre coincide exactamente con alguno de «descartar» y los pulsa con su propia acción
        /// (InvokePattern), sin mover el mouse del usuario. Devuelve los nombres de los botones pulsados.
        /// </summary>
        public static List<string> Descartar(IntPtr hwnd, string descartar)
        {
            var pulsados = new List<string>();
            if (hwnd == IntPtr.Zero || string.IsNullOrEmpty(descartar)) return pulsados;
            var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string n in descartar.Split('|')) if (n.Trim().Length > 0) nombres.Add(n.Trim());

            uint pid;
            Nativo.GetWindowThreadProcessId(hwnd, out pid);
            var botones = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
            // Las ventanas se abren por su identificador (handle): desde la raíz del escritorio algunas
            // (p. ej., las de WinForms) no exponen sus botones.
            foreach (IntPtr hv in Ventanas.DelProceso(pid))
            {
                AutomationElementCollection candidatos;
                try { candidatos = AutomationElement.FromHandle(hv).FindAll(TreeScope.Descendants, botones); }
                catch (ElementNotAvailableException) { continue; }
                catch (ArgumentException) { continue; }
                foreach (AutomationElement b in candidatos)
                {
                    try
                    {
                        var c = b.Current;
                        if (c.IsOffscreen || !c.IsEnabled || !nombres.Contains((c.Name ?? "").Trim())) continue;
                        object patron;
                        if (!b.TryGetCurrentPattern(InvokePattern.Pattern, out patron)) continue;
                        string nombre = c.Name.Trim();
                        // Si el botón cierra su propia ventana, el elemento desaparece durante la acción: eso es éxito
                        try { ((InvokePattern)patron).Invoke(); } catch (ElementNotAvailableException) { }
                        pulsados.Add(nombre);
                    }
                    catch (ElementNotAvailableException) { }
                    catch (InvalidOperationException) { }
                }
            }
            if (pulsados.Count > 0) Thread.Sleep(500); // que la ventana emergente termine de cerrarse antes de capturar
            return pulsados;
        }
    }

    static class Analizador
    {
        public class Resultado
        {
            public string Resumen = "", Archivo = "";
            public int Elementos;
        }

        /// <summary>Textos visibles (títulos, etiquetas, párrafos cortos) en orden de lectura, sin repetir.</summary>
        static List<string> Textos(IntPtr hwnd, int maximo)
        {
            var lista = new List<string>();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var cond = new OrCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                                           new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Header),
                                           new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.HeaderItem));
                var items = new List<KeyValuePair<System.Windows.Rect, string>>();
                foreach (AutomationElement e in AutomationElement.FromHandle(hwnd).FindAll(TreeScope.Descendants, cond))
                {
                    try
                    {
                        var c = e.Current;
                        string t = (c.Name ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
                        if (c.IsOffscreen || t.Length < 2 || t.Length > 160 || !vistos.Add(t)) continue;
                        items.Add(new KeyValuePair<System.Windows.Rect, string>(c.BoundingRectangle, t));
                    }
                    catch (ElementNotAvailableException) { }
                }
                items.Sort((a, b) => Math.Abs(a.Key.Top - b.Key.Top) > 8 ? a.Key.Top.CompareTo(b.Key.Top) : a.Key.Left.CompareTo(b.Key.Left));
                foreach (var it in items) { lista.Add(it.Value); if (lista.Count >= maximo) break; }
            }
            catch (ElementNotAvailableException) { }
            return lista;
        }

        static string Plural(int n, string uno, string varios) { return n + " " + (n == 1 ? uno : varios); }

        static readonly Dictionary<string, string> Plurales = new Dictionary<string, string> {
            { "botón", "botones" }, { "enlace", "enlaces" }, { "campo", "campos" }, { "casilla", "casillas" },
            { "opción", "opciones" }, { "lista desplegable", "listas desplegables" }, { "menú", "menús" },
            { "pestaña", "pestañas" }, { "elemento de lista", "elementos de lista" }, { "control deslizante", "controles deslizantes" },
            { "nodo", "nodos" }, { "fila", "filas" } };

        /// <summary>
        /// Analiza la ventana: título, programa, textos visibles y elementos interactivos. Escribe «…_analisis.md»
        /// junto a la imagen (mismo nombre base) y devuelve un resumen en una o dos frases.
        /// </summary>
        public static Resultado Analizar(IntPtr hwnd, string rutaImagen)
        {
            var r = new Resultado();
            if (hwnd != IntPtr.Zero && !Nativo.IsWindowVisible(hwnd)) hwnd = IntPtr.Zero; // la ventana ya se cerró
            if (hwnd == IntPtr.Zero)
            {
                r.Resumen = "No había una ventana activa que analizar; se guardó solo la imagen.";
                return r;
            }
            var info = InfoVentana.De(hwnd);
            List<Elemento> elementos = hwnd == IntPtr.Zero ? new List<Elemento>() : Elementos.Listar(hwnd);
            List<string> textos = hwnd == IntPtr.Zero ? new List<string>() : Textos(hwnd, 40);
            r.Elementos = elementos.Count;

            var cuenta = new SortedDictionary<string, int>();
            foreach (var e in elementos) { int n; cuenta.TryGetValue(e.Tipo, out n); cuenta[e.Tipo] = n + 1; }
            var partes = new List<string>();
            foreach (var kv in cuenta)
            {
                string varios;
                if (!Plurales.TryGetValue(kv.Key, out varios)) varios = kv.Key + "s";
                partes.Add(Plural(kv.Value, kv.Key, varios));
            }

            string programa = info.Proceso.Length > 0 ? " (" + info.Proceso + ")" : "";
            var sb = new StringBuilder();
            sb.Append("Se observa la ventana «" + info.Titulo + "»" + programa + ". ");
            sb.Append(elementos.Count == 0 ? "No se detectaron elementos interactivos. " : "Contiene " + string.Join(", ", partes.ToArray()) + ". ");
            if (textos.Count > 0)
            {
                var principales = textos.GetRange(0, Math.Min(4, textos.Count));
                sb.Append("Textos principales: «" + string.Join("», «", principales.ToArray()) + "».");
            }
            r.Resumen = sb.ToString().Trim();

            var md = new StringBuilder();
            md.AppendLine("# Análisis de «" + Path.GetFileName(rutaImagen) + "»");
            md.AppendLine();
            md.AppendLine("**Resumen.** " + r.Resumen);
            md.AppendLine();
            md.AppendLine("- Ventana: «" + info.Titulo + "»");
            md.AppendLine("- Programa: " + (info.Proceso.Length > 0 ? info.Proceso : "desconocido"));
            md.AppendLine("- Fecha: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            md.AppendLine();
            if (textos.Count > 0)
            {
                md.AppendLine("## Textos visibles");
                md.AppendLine();
                foreach (string t in textos) md.AppendLine("- " + t);
                md.AppendLine();
            }
            if (elementos.Count > 0)
            {
                md.AppendLine("## Elementos interactivos");
                md.AppendLine();
                md.AppendLine("| N.º | Tipo | Nombre | Centro (x, y) | Habilitado |");
                md.AppendLine("|---|---|---|---|---|");
                foreach (var e in elementos)
                    md.AppendLine("| " + e.Numero + " | " + e.Tipo + " | " + e.Nombre.Replace("|", "/") + " | " + e.Centro.X + ", " + e.Centro.Y + " | " + (e.Habilitado ? "sí" : "no") + " |");
            }
            r.Archivo = Path.Combine(Path.GetDirectoryName(rutaImagen), Path.GetFileNameWithoutExtension(rutaImagen) + "_analisis.md");
            File.WriteAllText(r.Archivo, md.ToString(), new UTF8Encoding(false));
            return r;
        }
    }
}
