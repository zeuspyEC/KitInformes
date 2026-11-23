// Motor de guiones: ejecuta pasos (abrir, clic por nombre, escribir, verificar…) sobre ventanas reales,
// verifica cada uno con capturas antes/después y redacta el registro en voz impersonal para el informe.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace KitInformes
{
    static class Ventanas
    {
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern void keybd_event(byte k, byte s, int f, int e);

        /// <summary>Primera ventana visible cuyo título contiene el texto (sin distinguir mayúsculas).</summary>
        /// <summary>Ventanas visibles de primer nivel de un proceso (la principal y sus ventanas emergentes).</summary>
        public static List<IntPtr> DelProceso(uint pid)
        {
            var lista = new List<IntPtr>();
            EnumWindows((h, l) =>
            {
                uint p;
                Nativo.GetWindowThreadProcessId(h, out p);
                if (p == pid && Nativo.IsWindowVisible(h)) lista.Add(h);
                return true;
            }, IntPtr.Zero);
            return lista;
        }

        public static IntPtr Buscar(string texto)
        {
            IntPtr hallada = IntPtr.Zero;
            string t = texto.ToLowerInvariant();
            EnumWindows((h, l) =>
            {
                if (!Nativo.IsWindowVisible(h)) return true;
                var sb = new StringBuilder(512);
                Nativo.GetWindowText(h, sb, sb.Capacity);
                if (sb.Length > 0 && sb.ToString().ToLowerInvariant().Contains(t)) { hallada = h; return false; }
                return true;
            }, IntPtr.Zero);
            return hallada;
        }

        public static bool Traer(IntPtr h)
        {
            if (h == IntPtr.Zero) return false;
            if (Nativo.IsIconic(h)) ShowWindow(h, 9);
            if (Nativo.GetForegroundWindow() == h) return true;
            keybd_event(0x12, 0, 0, 0); SetForegroundWindow(h); keybd_event(0x12, 0, 2, 0);
            Thread.Sleep(300);
            return Nativo.GetForegroundWindow() == h;
        }

        public static void Maximizar(IntPtr h) { ShowWindow(h, 3); Thread.Sleep(600); }
    }

    class Guion
    {
        class Paso { public int N; public string Instruccion, Descripcion, Imagen = ""; public bool Ok; }

        readonly string carpeta, descartar;
        readonly List<Paso> pasos = new List<Paso>();
        IntPtr ventana;

        public Guion(string carpetaBase, string descartar)
        {
            this.descartar = descartar;
            carpeta = Path.Combine(carpetaBase, "guion_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(carpeta);
        }

        public string Carpeta { get { return carpeta; } }

        static string EscaparSendKeys(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                if ("+^%~(){}[]".IndexOf(c) >= 0) sb.Append('{').Append(c).Append('}'); else sb.Append(c);
            return sb.ToString();
        }

        /// <summary>«ENTER» → {ENTER}; «CTRL+A» → ^a; «CTRL+SHIFT+T» → ^+t; «ALT+F4» → %{F4}.</summary>
        static string Combinacion(string teclas)
        {
            var sb = new StringBuilder();
            string[] partes = teclas.ToUpperInvariant().Split('+');
            for (int i = 0; i < partes.Length - 1; i++)
            {
                string m = partes[i].Trim();
                sb.Append(m == "CTRL" || m == "CONTROL" ? "^" : m == "ALT" ? "%" : m == "SHIFT" || m == "MAYUS" ? "+" : "");
            }
            string k = partes[partes.Length - 1].Trim();
            sb.Append(k.Length == 1 ? k.ToLowerInvariant() : "{" + k + "}");
            return sb.ToString();
        }

        string CapturarVentana(string sufijo)
        {
            if (ventana == IntPtr.Zero) return "";
            using (Bitmap b = Captura.Ventana(ventana))
            {
                if (b == null) return "";
                string f = string.Format("{0:00}_{1}.png", pasos.Count + 1, sufijo);
                b.Save(Path.Combine(carpeta, f), ImageFormat.Png);
                return f;
            }
        }

        List<Elemento> EsperarElemento(string nombre, string tipo, int segundos, out Elemento hallado)
        {
            var reloj = Stopwatch.StartNew();
            List<Elemento> lista;
            do
            {
                lista = Elementos.Listar(ventana);
                hallado = Elementos.Buscar(lista, nombre, tipo);
                if (hallado != null) return lista;
                Thread.Sleep(500);
            } while (reloj.Elapsed.TotalSeconds < segundos);
            return lista;
        }

        static void Separar(string resto, out string nombre, out string tipo)
        {
            int i = resto.IndexOf('|');
            nombre = (i >= 0 ? resto.Substring(0, i) : resto).Trim();
            tipo = i >= 0 ? resto.Substring(i + 1).Trim() : "";
        }

        /// <summary>Ejecuta el guion; se detiene en el primer paso que falla. Devuelve 0 o el número del paso fallido.</summary>
        public int Ejecutar(string archivo)
        {
            ventana = Nativo.GetForegroundWindow();
            int fallo = 0;
            foreach (string cruda in File.ReadAllLines(archivo, Encoding.UTF8))
            {
                string linea = cruda.Trim();
                if (linea.Length == 0 || linea.StartsWith("#")) continue;
                int sp = linea.IndexOf(' ');
                string cmd = (sp < 0 ? linea : linea.Substring(0, sp)).ToLowerInvariant();
                string resto = sp < 0 ? "" : linea.Substring(sp + 1).Trim();
                var p = new Paso { N = pasos.Count + 1, Instruccion = cmd == "escribir-oculto" ? "escribir-oculto ••••" : linea };
                // Antes de cada acción sobre la ventana se cierran ventanas emergentes («Nunca», «No, gracias»…)
                // para que no tapen el clic ni salgan en las capturas
                List<string> cerrados = AccionesQueLimpian.Contains(cmd) ? Limpieza.Descartar(ventana, descartar) : new List<string>();
                try { EjecutarPaso(cmd, resto, p); }
                catch (Exception ex) { p.Ok = false; p.Descripcion = (p.Descripcion ?? "Error en «" + linea + "»") + " (" + ex.Message + ")"; }
                if (cerrados.Count > 0) p.Descripcion = "Se cerró una ventana emergente con «" + string.Join("», «", cerrados.ToArray()) + "». " + p.Descripcion;
                pasos.Add(p);
                if (!p.Ok) { fallo = p.N; break; }
            }
            EscribirRegistro(archivo, fallo);
            return fallo;
        }

        static readonly HashSet<string> AccionesQueLimpian = new HashSet<string> {
            "clic-nombre", "clic", "escribir", "escribir-oculto", "tecla", "elementos", "analizar", "verificar" };

        void EjecutarPaso(string cmd, string resto, Paso p)
        {
            string nombre, tipo;
            switch (cmd)
            {
                case "abrir":
                {
                    string exe = resto, args = "";
                    if (resto.StartsWith("\"")) { int c = resto.IndexOf('"', 1); exe = resto.Substring(1, c - 1); args = resto.Substring(c + 1).Trim(); }
                    else { int c = resto.IndexOf(' '); if (c > 0) { exe = resto.Substring(0, c); args = resto.Substring(c + 1); } }
                    Process.Start(exe, args);
                    p.Ok = true;
                    p.Descripcion = "Se abrió " + Path.GetFileNameWithoutExtension(exe) + (args.Length > 0 ? " con «" + args + "»" : "") + ".";
                    break;
                }
                case "ventana":
                {
                    var reloj = Stopwatch.StartNew();
                    IntPtr h;
                    while ((h = Ventanas.Buscar(resto)) == IntPtr.Zero && reloj.Elapsed.TotalSeconds < 20) Thread.Sleep(400);
                    p.Ok = h != IntPtr.Zero && Ventanas.Traer(h);
                    if (h != IntPtr.Zero) ventana = h;
                    p.Descripcion = p.Ok ? "Se trabajó sobre la ventana «" + InfoVentana.De(h).Titulo + "»." : "No se encontró la ventana «" + resto + "».";
                    break;
                }
                case "maximizar":
                    Ventanas.Maximizar(ventana); p.Ok = true; p.Descripcion = "Se maximizó la ventana."; break;
                case "esperar":
                    Thread.Sleep(int.Parse(resto)); p.Ok = true; p.Descripcion = "Se esperó " + resto + " ms."; break;
                case "capturar":
                {
                    // Pantalla limpia: antes de la foto se cierran ventanas emergentes («Nunca», «No, gracias»…)
                    var cerrados = Limpieza.Descartar(ventana, descartar);
                    Ventanas.Traer(ventana); Thread.Sleep(200);
                    p.Imagen = CapturarVentana("captura"); p.Ok = p.Imagen.Length > 0;
                    p.Descripcion = (resto.Length > 0 ? resto : "Se capturó la ventana") + "." +
                        (cerrados.Count > 0 ? " Antes se cerró una ventana emergente con «" + string.Join("», «", cerrados.ToArray()) + "»." : "");
                    break;
                }
                case "limpiar":
                {
                    var cerrados = Limpieza.Descartar(ventana, descartar);
                    p.Ok = true;
                    p.Descripcion = cerrados.Count > 0 ? "Se cerraron ventanas emergentes con «" + string.Join("», «", cerrados.ToArray()) + "»."
                                                       : "No había ventanas emergentes que cerrar.";
                    break;
                }
                case "analizar":
                {
                    Ventanas.Traer(ventana); Thread.Sleep(200);
                    p.Imagen = CapturarVentana("analisis");
                    var r = Analizador.Analizar(ventana, Path.Combine(carpeta, p.Imagen));
                    p.Ok = p.Imagen.Length > 0;
                    p.Descripcion = r.Resumen;
                    break;
                }
                case "elementos":
                {
                    Ventanas.Traer(ventana);
                    var lista = Elementos.Listar(ventana);
                    string csv;
                    p.Imagen = Path.GetFileName(Elementos.Documentar(ventana, lista, carpeta, out csv));
                    p.Ok = lista.Count > 0;
                    p.Descripcion = (lista.Count == 1 ? "Se identificó 1 elemento interactivo" : "Se identificaron " + lista.Count + " elementos interactivos") + " en la ventana (" + Path.GetFileName(csv) + ").";
                    break;
                }
                case "clic-nombre":
                {
                    Separar(resto, out nombre, out tipo);
                    Ventanas.Traer(ventana);
                    Elemento e;
                    EsperarElemento(nombre, tipo, 10, out e);
                    if (e == null) { p.Ok = false; p.Descripcion = "No se encontró «" + nombre + "» en la ventana."; break; }
                    var r = Accion.Ejecutar("clic", e.Centro.X, e.Centro.Y, 1500, carpeta);
                    p.Imagen = Path.GetFileName(r.Marcado);
                    p.Ok = r.Cambio;
                    p.Descripcion = "Se hizo clic en el " + e.Tipo + " «" + e.Nombre + "»" +
                        (r.Cambio ? " y la pantalla cambió (" + r.Global.ToString("0.#") + " %)." : ", pero la pantalla no cambió.");
                    break;
                }
                case "clic-si-existe":
                {
                    // Para diálogos que pueden aparecer o no (p. ej., «¿Guardar la contraseña?» del navegador)
                    Separar(resto, out nombre, out tipo);
                    Ventanas.Traer(ventana);
                    Elemento e;
                    EsperarElemento(nombre, tipo, 3, out e);
                    p.Ok = true;
                    if (e == null) { p.Descripcion = "No apareció «" + nombre + "», por lo que no fue necesario cerrarlo."; break; }
                    var r = Accion.Ejecutar("clic", e.Centro.X, e.Centro.Y, 1000, carpeta);
                    p.Imagen = Path.GetFileName(r.Marcado);
                    p.Ok = r.Cambio;
                    p.Descripcion = "Apareció el " + e.Tipo + " «" + e.Nombre + "» y se hizo clic en él" + (r.Cambio ? "." : ", pero la pantalla no cambió.");
                    break;
                }
                case "clic":
                {
                    string[] xy = resto.Split(' ');
                    Ventanas.Traer(ventana);
                    var r = Accion.Ejecutar("clic", int.Parse(xy[0]), int.Parse(xy[1]), 1200, carpeta);
                    p.Imagen = Path.GetFileName(r.Marcado); p.Ok = r.Cambio;
                    p.Descripcion = "Se hizo clic en (" + xy[0] + ", " + xy[1] + ")" + (r.Cambio ? " y la pantalla cambió." : ", pero la pantalla no cambió.");
                    break;
                }
                case "escribir":
                case "escribir-oculto":
                    if (!Ventanas.Traer(ventana)) { p.Ok = false; p.Descripcion = "La ventana no quedó en primer plano para escribir."; break; }
                    SendKeys.SendWait(EscaparSendKeys(resto)); Thread.Sleep(300);
                    p.Ok = true;
                    p.Descripcion = cmd == "escribir" ? "Se escribió «" + resto + "»." : "Se escribió la contraseña.";
                    break;
                case "tecla":
                    if (!Ventanas.Traer(ventana)) { p.Ok = false; p.Descripcion = "La ventana no quedó en primer plano."; break; }
                    SendKeys.SendWait(Combinacion(resto)); Thread.Sleep(800);
                    p.Ok = true;
                    p.Descripcion = resto.Contains("+") ? "Se presionó la combinación " + resto.ToUpperInvariant() + "." : "Se presionó la tecla " + resto.ToUpperInvariant() + ".";
                    break;
                case "verificar":
                {
                    Separar(resto, out nombre, out tipo);
                    Elemento e;
                    EsperarElemento(nombre, tipo, 15, out e);
                    p.Ok = e != null;
                    p.Imagen = CapturarVentana("verificacion");
                    p.Descripcion = p.Ok ? "Se verificó que aparece el " + e.Tipo + " «" + e.Nombre + "»." : "No apareció «" + nombre + "».";
                    break;
                }
                case "verificar-titulo":
                {
                    var reloj = Stopwatch.StartNew();
                    string titulo;
                    while (!(titulo = InfoVentana.De(ventana).Titulo).ToLowerInvariant().Contains(resto.ToLowerInvariant()) && reloj.Elapsed.TotalSeconds < 15) Thread.Sleep(400);
                    p.Ok = titulo.ToLowerInvariant().Contains(resto.ToLowerInvariant());
                    p.Imagen = CapturarVentana("verificacion");
                    p.Descripcion = p.Ok ? "Se verificó que el título de la ventana es «" + titulo + "»." : "El título «" + titulo + "» no contiene «" + resto + "».";
                    break;
                }
                case "cerrar":
                    Ventanas.Traer(ventana); SendKeys.SendWait("^w"); Thread.Sleep(600);
                    p.Ok = true; p.Descripcion = "Se cerró la pestaña."; break;
                default:
                    p.Ok = false; p.Descripcion = "Instrucción desconocida: «" + cmd + "».";
                    break;
            }
        }

        void EscribirRegistro(string archivo, int fallo)
        {
            var md = new StringBuilder();
            md.AppendLine("# Registro del guion «" + Path.GetFileName(archivo) + "»");
            md.AppendLine();
            md.AppendLine("Fecha: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " · Resultado: " +
                (fallo == 0 ? "**todos los pasos correctos** (" + pasos.Count + ")" : "**falló el paso " + fallo + "**"));
            md.AppendLine();
            var csv = new StringBuilder("paso;instruccion;resultado;descripcion;imagen\r\n");
            foreach (var p in pasos)
            {
                md.AppendLine("**Paso " + p.N + ".** " + p.Descripcion + (p.Ok ? "" : " ❌"));
                md.AppendLine();
                if (p.Imagen.Length > 0) { md.AppendLine("![Paso " + p.N + "](" + p.Imagen + ")"); md.AppendLine(); }
                csv.AppendLine(string.Join(";", new[] { p.N.ToString(), "\"" + p.Instruccion.Replace("\"", "\"\"") + "\"", p.Ok ? "OK" : "FALLA",
                    "\"" + p.Descripcion.Replace("\"", "\"\"") + "\"", p.Imagen }));
            }
            File.WriteAllText(Path.Combine(carpeta, "registro.md"), md.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(carpeta, "pasos.csv"), csv.ToString(), new UTF8Encoding(true));
        }
    }
}
