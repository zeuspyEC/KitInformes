// Comandos de KitInformes.exe para la terminal: leer lo que se ve (sin IA), analizar, limpiar ventanas
// emergentes, explorar por hover, clic por nombre y guiones. El mismo .exe es el capturador residente.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace KitInformes
{
    static class Comandos
    {
        public static readonly string[] Nombres = { "elementos", "analizar", "limpiar", "clic-nombre", "explorar", "guion", "ayuda" };

        const string AYUDA =
@"KitInformes — comandos:
  KitInformes                                         abre el capturador (botón flotante, atajos, configuración)
  KitInformes --capturar pantalla|ventana             una captura y termina
  KitInformes analizar [--ventana ""título""]           captura + contexto de lo que se ve (_analisis.md)
  KitInformes limpiar [--ventana ""título""]            cierra ventanas emergentes («Nunca», «No, gracias»…)
  KitInformes elementos [--ventana ""título""]          botones, enlaces y campos (captura numerada + CSV)
  KitInformes clic-nombre ""texto"" [--tipo botón] [--ventana ""título""]   busca, hace clic y verifica
  KitInformes --sondear X Y | --clic X Y [--esperar ms]   ¿reacciona al hover? / clic verificado
  KitInformes explorar X Y ANCHO ALTO [--paso 24]     zonas interactivas por hover (solo visual)
  KitInformes guion pasos.txt                         ejecuta un guion y deja registro.md
  KitInformes --salir                                 cierra el capturador abierto
Códigos de salida: 0 correcto · 1 sin cambio / nada encontrado · 3 argumentos inválidos · 4 no encontrado
(en guion: número del paso que falló).";

        [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);

        /// <summary>El .exe es de ventanas: para escribir en la terminal desde la que se llamó, se adjunta a su consola.</summary>
        static void UsarConsola()
        {
            if (AttachConsole(-1))
            {
                var o = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
                Console.SetOut(o);
                Console.WriteLine();
            }
        }

        public static int Ejecutar(List<string> lista, string dirBase)
        {
            UsarConsola();
            var cfg = Config.Cargar(dirBase);
            string carpeta = cfg.Carpeta;
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
                case "analizar":
                {
                    IntPtr h = objetivo();
                    if (h == IntPtr.Zero) { Console.WriteLine("No se encontró la ventana."); return 4; }
                    if (cfg.Limpiar) foreach (string b in Limpieza.Descartar(h, cfg.Descartar)) Console.WriteLine("Se cerró una ventana emergente con «" + b + "».");
                    var info = InfoVentana.De(h);
                    string ruta;
                    using (Bitmap bmp = Captura.Ventana(h))
                    {
                        if (bmp == null) { Console.WriteLine("La ventana está minimizada."); return 1; }
                        ruta = new Registro(carpeta).Guardar(bmp, "ventana", info);
                    }
                    var r = Analizador.Analizar(h, ruta);
                    Console.WriteLine(r.Resumen);
                    Console.WriteLine("Imagen: " + ruta);
                    Console.WriteLine("Análisis: " + r.Archivo);
                    return 0;
                }
                case "limpiar":
                {
                    IntPtr h = objetivo();
                    var pulsados = Limpieza.Descartar(h, cfg.Descartar);
                    foreach (string b in pulsados) Console.WriteLine("Se cerró una ventana emergente con «" + b + "».");
                    if (pulsados.Count == 0) Console.WriteLine("No había ventanas emergentes que cerrar.");
                    return pulsados.Count > 0 ? 0 : 1;
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
                    var g = new Guion(carpeta, cfg.Descartar);
                    int fallo = g.Ejecutar(Path.GetFullPath(lista[1]));
                    Console.WriteLine(File.ReadAllText(Path.Combine(g.Carpeta, "registro.md"), Encoding.UTF8));
                    Console.WriteLine("Carpeta: " + g.Carpeta);
                    return fallo;
                }
            }
            Console.WriteLine(AYUDA);
            return lista[0] == "ayuda" ? 0 : 3;
        }
    }
}
