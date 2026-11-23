// Automatizador — KitInformes
// Programa de consola, separado del Capturador, para leer la pantalla sin IA (accesibilidad de Windows),
// explorar por hover, hacer clic por nombre y ejecutar guiones. Se ejecuta solo cuando se le pide.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Automatizador - KitInformes")]
[assembly: System.Reflection.AssemblyDescription("Lectura de elementos de pantalla, clic verificado y guiones para documentar procedimientos")]
[assembly: System.Reflection.AssemblyProduct("KitInformes")]
[assembly: System.Reflection.AssemblyCompany("Erick Costa")]
[assembly: System.Reflection.AssemblyVersion("1.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.2.0.0")]

namespace KitInformes
{
    static class Automatizador
    {
        const string AYUDA =
@"Automatizador (KitInformes) — uso:
  Automatizador elementos [--ventana ""título""]          lista botones, enlaces y campos (captura numerada + CSV)
  Automatizador clic-nombre ""texto"" [--tipo botón] [--ventana ""título""]   busca, hace clic y verifica
  Automatizador sondear X Y                              ¿reacciona al pasar el cursor?
  Automatizador clic X Y [--esperar ms]                  clic con verificación antes/después
  Automatizador explorar X Y ANCHO ALTO [--paso 24]      zonas interactivas por hover (solo visual)
  Automatizador guion pasos.txt                          ejecuta un guion y deja registro.md
Códigos de salida: 0 correcto · 1 sin cambio / nada encontrado · 3 argumentos inválidos · 4 elemento no encontrado
(en guion: número del paso que falló).";

        [STAThread]
        static int Main(string[] args)
        {
            Nativo.SetProcessDPIAware();
            Console.OutputEncoding = Encoding.UTF8;
            var lista = new List<string>(args);
            if (lista.Count == 0 || lista[0] == "-h" || lista[0] == "--ayuda") { Console.WriteLine(AYUDA); return lista.Count == 0 ? 3 : 0; }

            string dirBase = Path.GetDirectoryName(Application.ExecutablePath);
            string carpeta = Config.Cargar(dirBase).Carpeta;
            Func<string, string> valor = (op) => { int k = lista.IndexOf(op); return k >= 0 && k + 1 < lista.Count ? lista[k + 1] : null; };
            Func<int, int?> entero = (k) => { int n; return k < lista.Count && int.TryParse(lista[k], out n) ? (int?)n : null; };
            Func<IntPtr> objetivo = () =>
            {
                string t = valor("--ventana");
                IntPtr h = t != null ? Ventanas.Buscar(t) : Nativo.GetForegroundWindow();
                if (t != null && !Ventanas.Traer(h)) Console.WriteLine("Aviso: la ventana «" + t + "» no quedó en primer plano.");
                return h;
            };

            switch (lista[0].TrimStart('-').ToLowerInvariant())
            {
                case "elementos":
                {
                    IntPtr h = objetivo();
                    if (h == IntPtr.Zero) { Console.WriteLine("No se encontró la ventana."); return 4; }
                    var els = Elementos.Listar(h);
                    string csv, png = Elementos.Documentar(h, els, carpeta, out csv);
                    Console.WriteLine("Ventana: " + InfoVentana.De(h).Titulo);
                    foreach (var e in els)
                        Console.WriteLine(string.Format("{0,3}. {1,-16} «{2}»  centro ({3}, {4})", e.Numero, e.Tipo, e.Nombre, e.Centro.X, e.Centro.Y));
                    Console.WriteLine(els.Count + " elementos · " + png);
                    return els.Count > 0 ? 0 : 1;
                }
                case "clic-nombre":
                {
                    if (lista.Count < 2) return 3;
                    IntPtr h = objetivo();
                    var el = Elementos.Buscar(Elementos.Listar(h), lista[1], valor("--tipo") ?? "");
                    if (el == null) { Console.WriteLine("No se encontró «" + lista[1] + "»."); return 4; }
                    var r = Accion.Ejecutar("clic", el.Centro.X, el.Centro.Y, 1500, carpeta);
                    Console.WriteLine("Clic en el " + el.Tipo + " «" + el.Nombre + "» (" + el.Centro.X + ", " + el.Centro.Y + "): " +
                        (r.Cambio ? "la pantalla cambió" : "sin cambio") + " · " + r.Marcado);
                    return r.Cambio ? 0 : 1;
                }
                case "sondear":
                case "clic":
                {
                    int? x = entero(1), y = entero(2);
                    if (x == null || y == null) return 3;
                    int espera = lista[0].Contains("clic") ? 1200 : 600;
                    if (valor("--esperar") != null) int.TryParse(valor("--esperar"), out espera);
                    var r = Accion.Ejecutar(lista[0].TrimStart('-').ToLowerInvariant(), x.Value, y.Value, espera, carpeta);
                    Console.WriteLine((r.Cambio ? "Cambió" : "Sin cambio") + " · local " + r.Porcentaje.ToString("0.###") + " % · global " +
                        r.Global.ToString("0.###") + " % · zona " + r.Zona + " · " + r.Marcado);
                    return r.Cambio ? 0 : 1;
                }
                case "explorar":
                {
                    int? x = entero(1), y = entero(2), w = entero(3), alto = entero(4);
                    if (x == null || y == null || w == null || alto == null) return 3;
                    int paso = 24;
                    if (valor("--paso") != null) int.TryParse(valor("--paso"), out paso);
                    string marcado;
                    var cajas = Elementos.Explorar(new Rectangle(x.Value, y.Value, w.Value, alto.Value), Math.Max(6, paso), carpeta, out marcado);
                    var sb = new StringBuilder("zona;x;y;ancho;alto;centro_x;centro_y\r\n");
                    for (int k = 0; k < cajas.Count; k++)
                    {
                        Rectangle c = cajas[k];
                        sb.AppendLine(string.Join(";", new[] { (k + 1).ToString(), c.X.ToString(), c.Y.ToString(), c.Width.ToString(), c.Height.ToString(),
                            (c.X + c.Width / 2).ToString(), (c.Y + c.Height / 2).ToString() }));
                        Console.WriteLine(string.Format("Zona {0}: {1}", k + 1, c));
                    }
                    File.WriteAllText(Path.ChangeExtension(marcado, ".csv"), sb.ToString(), new UTF8Encoding(true));
                    Console.WriteLine(cajas.Count + " zonas interactivas · " + marcado);
                    return cajas.Count > 0 ? 0 : 1;
                }
                case "guion":
                {
                    if (lista.Count < 2 || !File.Exists(lista[1])) { Console.WriteLine("No existe el guion."); return 3; }
                    var g = new Guion(carpeta);
                    int fallo = g.Ejecutar(Path.GetFullPath(lista[1]));
                    Console.WriteLine(File.ReadAllText(Path.Combine(g.Carpeta, "registro.md"), Encoding.UTF8));
                    Console.WriteLine("Carpeta: " + g.Carpeta);
                    return fallo;
                }
            }
            Console.WriteLine(AYUDA);
            return 3;
        }
    }
}
