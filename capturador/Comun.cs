// Piezas comunes del Capturador y del Automatizador: configuración, captura de pantalla,
// registro numerado y ciclo «actuar y verificar» (antes/después).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace KitInformes
{
    static class Nativo
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);

        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;
        public const int WM_HOTKEY = 0x0312, WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
        public const int WS_EX_TOOLWINDOW = 0x80, WS_EX_TOPMOST = 0x8, WS_EX_NOACTIVATE = 0x08000000;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
    }

    /// <summary>Configuración portable: config.ini junto al ejecutable.</summary>
    class Config
    {
        public string Carpeta;
        public int X = int.MinValue, Y = int.MinValue;
        public bool Visible = true, Portapapeles = true, Sonido = false;
        /// <summary>Comando opcional que se ejecuta tras cada captura ({archivo} = ruta del PNG). Punto de extensión para el análisis con IA.</summary>
        public string Despues = "";
        readonly string ruta;

        Config(string ruta) { this.ruta = ruta; }

        public static Config Cargar(string dirBase)
        {
            var c = new Config(Path.Combine(dirBase, "config.ini"));
            c.Carpeta = Path.Combine(dirBase, "Capturas");
            if (File.Exists(c.ruta))
            {
                foreach (var linea in File.ReadAllLines(c.ruta, Encoding.UTF8))
                {
                    int i = linea.IndexOf('=');
                    if (i <= 0 || linea.TrimStart().StartsWith(";")) continue;
                    string k = linea.Substring(0, i).Trim().ToLowerInvariant(), v = linea.Substring(i + 1).Trim();
                    int n;
                    switch (k)
                    {
                        case "carpeta": if (v.Length > 0) c.Carpeta = Path.IsPathRooted(v) ? v : Path.Combine(dirBase, v); break;
                        case "x": if (int.TryParse(v, out n)) c.X = n; break;
                        case "y": if (int.TryParse(v, out n)) c.Y = n; break;
                        case "visible": c.Visible = v != "0"; break;
                        case "portapapeles": c.Portapapeles = v != "0"; break;
                        case "sonido": c.Sonido = v == "1"; break;
                        case "despues": c.Despues = v; break;
                    }
                }
            }
            return c;
        }

        public void Guardar()
        {
            var sb = new StringBuilder();
            sb.AppendLine("; Configuración del Capturador (se guarda sola)");
            sb.AppendLine("carpeta=" + Carpeta);
            sb.AppendLine("x=" + X);
            sb.AppendLine("y=" + Y);
            sb.AppendLine("visible=" + (Visible ? 1 : 0));
            sb.AppendLine("portapapeles=" + (Portapapeles ? 1 : 0));
            sb.AppendLine("sonido=" + (Sonido ? 1 : 0));
            sb.AppendLine("; Comando a ejecutar después de cada captura; {archivo} se reemplaza por la ruta del PNG");
            sb.AppendLine("despues=" + Despues);
            try { File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(true)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Datos de la ventana que estaba activa al momento de capturar.</summary>
    class InfoVentana
    {
        public IntPtr Handle = IntPtr.Zero;
        public string Proceso = "";
        public string Titulo = "";

        public static InfoVentana De(IntPtr h)
        {
            var info = new InfoVentana { Handle = h };
            if (h == IntPtr.Zero) return info;
            var sb = new StringBuilder(512);
            Nativo.GetWindowText(h, sb, sb.Capacity);
            info.Titulo = sb.ToString();
            uint pid;
            Nativo.GetWindowThreadProcessId(h, out pid);
            try { using (var p = Process.GetProcessById((int)pid)) info.Proceso = p.ProcessName; } catch (ArgumentException) { } catch (InvalidOperationException) { }
            return info;
        }
    }

    static class Captura
    {
        public static Bitmap Pantalla()
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            return Area(vs);
        }

        public static Bitmap Area(Rectangle r)
        {
            var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Left, r.Top, 0, 0, r.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        /// <summary>Marco visible de la ventana (sin el borde invisible de Windows 10).</summary>
        public static Rectangle MarcoVentana(IntPtr h)
        {
            Nativo.RECT r;
            if (Nativo.DwmGetWindowAttribute(h, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out r, Marshal.SizeOf(typeof(Nativo.RECT))) != 0)
                Nativo.GetWindowRect(h, out r);
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        public static Bitmap Ventana(IntPtr h)
        {
            if (h == IntPtr.Zero || Nativo.IsIconic(h)) return null;
            Rectangle r = Rectangle.Intersect(MarcoVentana(h), SystemInformation.VirtualScreen);
            if (r.Width < 2 || r.Height < 2) return null;
            return Area(r);
        }
    }

    /// <summary>Guarda cada captura numerada y deja su información en capturas.csv.</summary>
    class Registro
    {
        public string Carpeta;
        public Registro(string carpeta) { Carpeta = carpeta; }

        public int SiguienteNumero()
        {
            int max = 0;
            if (Directory.Exists(Carpeta))
                foreach (var f in Directory.GetFiles(Carpeta, "*.png"))
                {
                    var m = Regex.Match(Path.GetFileName(f), @"^(\d+)_");
                    int n;
                    if (m.Success && int.TryParse(m.Groups[1].Value, out n) && n > max) max = n;
                }
            return max + 1;
        }

        static string Sanear(string s)
        {
            s = Regex.Replace(s ?? "", @"[^A-Za-z0-9\-]+", "");
            return s.Length > 30 ? s.Substring(0, 30) : s;
        }

        static string Csv(string s)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ");
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        public string Guardar(Bitmap bmp, string modo, InfoVentana info)
        {
            Directory.CreateDirectory(Carpeta);
            DateTime ahora = DateTime.Now;
            int n = SiguienteNumero();
            string sufijo = modo == "ventana" && info.Proceso.Length > 0 ? "ventana-" + Sanear(info.Proceso) : modo;
            string nombre = string.Format("{0:000}_{1:yyyy-MM-dd_HH-mm-ss}_{2}.png", n, ahora, sufijo);
            string ruta = Path.Combine(Carpeta, nombre);
            bmp.Save(ruta, ImageFormat.Png);

            string csv = Path.Combine(Carpeta, "capturas.csv");
            var sb = new StringBuilder();
            if (!File.Exists(csv)) sb.AppendLine("numero;fecha;hora;modo;archivo;ancho;alto;proceso;titulo_ventana");
            sb.AppendLine(string.Join(";", new[] {
                n.ToString("000"), ahora.ToString("yyyy-MM-dd"), ahora.ToString("HH:mm:ss"), modo, nombre,
                bmp.Width.ToString(), bmp.Height.ToString(), Csv(info.Proceso), Csv(info.Titulo) }));
            for (int intento = 0; ; intento++)
            {
                try { File.AppendAllText(csv, sb.ToString(), new UTF8Encoding(true)); break; }
                catch (IOException) { if (intento >= 5) throw; Thread.Sleep(200); } // p. ej., abierto en Excel
            }
            return ruta;
        }
    }

    static class Dibujo
    {
        public static readonly Color Fondo = Color.FromArgb(31, 41, 55), Hover = Color.FromArgb(47, 93, 138), Ok = Color.FromArgb(46, 125, 91);

        public static void Icono(Graphics g, int s, Color fondo, bool listo)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var b = new SolidBrush(fondo)) g.FillEllipse(b, 0, 0, s - 1, s - 1);
            using (var p = new Pen(Color.FromArgb(120, 255, 255, 255), Math.Max(1f, s / 28f))) g.DrawEllipse(p, 1, 1, s - 3, s - 3);
            if (listo)
            {
                using (var p = new Pen(Color.White, s / 9f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(p, new[] { new PointF(s * .28f, s * .52f), new PointF(s * .44f, s * .67f), new PointF(s * .73f, s * .36f) });
                return;
            }
            // Cámara
            float w = s * .54f, h = s * .36f, x = (s - w) / 2, y = s * .36f;
            using (var b = new SolidBrush(Color.White))
            {
                using (var path = Redondeado(new RectangleF(x, y, w, h), s * .06f)) g.FillPath(b, path);
                g.FillRectangle(b, x + w * .3f, y - s * .07f, w * .4f, s * .09f);
            }
            float lr = h * .62f;
            using (var b = new SolidBrush(fondo)) g.FillEllipse(b, s / 2f - lr / 2, y + h / 2 - lr / 2, lr, lr);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, s / 2f - lr / 4, y + h / 2 - lr / 4, lr / 2, lr / 2);
        }

        public static GraphicsPath Redondeado(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Icon CrearIcono(int s)
        {
            using (var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) Icono(g, s, Fondo, false);
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        /// <summary>Escribe un .ico con imágenes PNG de varios tamaños (para el ícono del .exe).</summary>
        public static void ExportarIco(string ruta)
        {
            int[] tamanos = { 16, 24, 32, 48, 64, 128, 256 };
            var pngs = new List<byte[]>();
            foreach (int s in tamanos)
                using (var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
                using (var ms = new MemoryStream())
                {
                    using (var g = Graphics.FromImage(bmp)) Icono(g, s, Fondo, false);
                    bmp.Save(ms, ImageFormat.Png);
                    pngs.Add(ms.ToArray());
                }
            using (var w = new BinaryWriter(File.Create(ruta)))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)tamanos.Length);
                int offset = 6 + 16 * tamanos.Length;
                for (int i = 0; i < tamanos.Length; i++)
                {
                    int s = tamanos[i];
                    w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s));
                    w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }
    }

    /// <summary>Pantalla congelada a pantalla completa para elegir una región con el mouse.</summary>
    static class Accion
    {
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern void mouse_event(int f, int x, int y, int d, int e);

        public class Resultado
        {
            public bool Cambio;
            public double Porcentaje, Global;
            public Rectangle Zona;
            public string Antes, Despues, Marcado;
        }

        /// <summary>Compara dos capturas del mismo tamaño; devuelve el % de píxeles distintos y el rectángulo que los contiene.</summary>
        public static double Comparar(Bitmap a, Bitmap b, Rectangle area, out Rectangle zona)
        {
            zona = Rectangle.Empty;
            if (a.Width != b.Width || a.Height != b.Height) { zona = new Rectangle(0, 0, b.Width, b.Height); return 100; }
            var r = new Rectangle(0, 0, a.Width, a.Height);
            area = Rectangle.Intersect(area, r);
            if (area.IsEmpty) return 0;
            BitmapData da = a.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData db = b.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int bytes = da.Stride * a.Height;
                byte[] pa = new byte[bytes], pb = new byte[bytes];
                Marshal.Copy(da.Scan0, pa, 0, bytes);
                Marshal.Copy(db.Scan0, pb, 0, bytes);
                int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                long distintos = 0;
                for (int y = area.Top; y < area.Bottom; y++)
                {
                    int fila = y * da.Stride;
                    for (int x = area.Left; x < area.Right; x++)
                    {
                        int i = fila + x * 4;
                        int d = Math.Abs(pa[i] - pb[i]) + Math.Abs(pa[i + 1] - pb[i + 1]) + Math.Abs(pa[i + 2] - pb[i + 2]);
                        if (d > 40)
                        {
                            distintos++;
                            if (x < minX) minX = x; if (x > maxX) maxX = x;
                            if (y < minY) minY = y; if (y > maxY) maxY = y;
                        }
                    }
                }
                if (maxX >= 0) zona = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
                return 100.0 * distintos / ((long)area.Width * area.Height);
            }
            finally { a.UnlockBits(da); b.UnlockBits(db); }
        }

        /// <summary>modo = "sondear" (solo pasar el cursor) o "clic".</summary>
        public static Resultado Ejecutar(string modo, int x, int y, int esperaMs, string carpeta)
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            // Cursor en reposo lejos del objetivo para la captura «antes» (el hover cambia la página).
            // Esquina superior izquierda: la inferior derecha es «Mostrar escritorio» en Windows 10.
            SetCursorPos(vs.Left + 1, vs.Top + 1);
            Thread.Sleep(350);
            var res = new Resultado();
            using (Bitmap antes = Captura.Pantalla())
            {
                SetCursorPos(x, y);
                if (modo == "clic") { Thread.Sleep(120); mouse_event(2, 0, 0, 0, 0); Thread.Sleep(60); mouse_event(4, 0, 0, 0, 0); }
                Thread.Sleep(esperaMs);
                using (Bitmap despues = Captura.Pantalla())
                {
                    Rectangle zona;
                    // Cambio local (200×200 alrededor del objetivo) y global; las animaciones lejanas (relojes, temporizadores) no cuentan como local
                    Rectangle zonaGlobal;
                    res.Global = Comparar(antes, despues, new Rectangle(0, 0, antes.Width, antes.Height), out zonaGlobal);
                    res.Porcentaje = Comparar(antes, despues, new Rectangle(x - vs.Left - 100, y - vs.Top - 100, 200, 200), out zona);
                    if (modo == "clic" && res.Global > 5) zona = zonaGlobal; // la pantalla cambió casi entera (navegación, pantalla completa)
                    res.Zona = zona;
                    res.Cambio = res.Porcentaje >= 0.01 || (modo == "clic" && res.Global > 1); // 0,01 % de 200×200 = 4 píxeles

                    Directory.CreateDirectory(carpeta);
                    var reg = new Registro(carpeta);
                    int n = reg.SiguienteNumero();
                    string marca = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    res.Antes = Path.Combine(carpeta, string.Format("{0:000}_{1}_{2}-antes.png", n, marca, modo));
                    res.Despues = Path.Combine(carpeta, string.Format("{0:000}_{1}_{2}-despues.png", n, marca, modo));
                    res.Marcado = Path.Combine(carpeta, string.Format("{0:000}_{1}_{2}-marcado.png", n, marca, modo));
                    antes.Save(res.Antes, ImageFormat.Png);
                    despues.Save(res.Despues, ImageFormat.Png);
                    using (var m = new Bitmap(despues))
                    {
                        using (var g = Graphics.FromImage(m))
                        {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            int px = x - vs.Left, py = y - vs.Top;
                            using (var p = new Pen(Color.FromArgb(220, 38, 38), 3))
                            {
                                g.DrawEllipse(p, px - 14, py - 14, 28, 28);
                                g.DrawLine(p, px - 22, py, px - 8, py); g.DrawLine(p, px + 8, py, px + 22, py);
                                g.DrawLine(p, px, py - 22, px, py - 8); g.DrawLine(p, px, py + 8, px, py + 22);
                            }
                            if (!zona.IsEmpty)
                                using (var p = new Pen(Color.FromArgb(56, 189, 248), 3) { DashStyle = DashStyle.Dash })
                                    g.DrawRectangle(p, Rectangle.Inflate(zona, 3, 3));
                        }
                        m.Save(res.Marcado, ImageFormat.Png);
                    }

                    string csv = Path.Combine(carpeta, "acciones.csv");
                    var sb = new StringBuilder();
                    if (!File.Exists(csv)) sb.AppendLine("numero;fecha;hora;accion;x;y;cambio;porcentaje_local;porcentaje_global;zona_x;zona_y;zona_ancho;zona_alto;antes;despues;marcado");
                    DateTime ahora = DateTime.Now;
                    sb.AppendLine(string.Join(";", new[] {
                        n.ToString("000"), ahora.ToString("yyyy-MM-dd"), ahora.ToString("HH:mm:ss"), modo, x.ToString(), y.ToString(),
                        res.Cambio ? "si" : "no", res.Porcentaje.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture), res.Global.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                        (zona.X + vs.Left).ToString(), (zona.Y + vs.Top).ToString(), zona.Width.ToString(), zona.Height.ToString(),
                        Path.GetFileName(res.Antes), Path.GetFileName(res.Despues), Path.GetFileName(res.Marcado) }));
                    File.AppendAllText(csv, sb.ToString(), new UTF8Encoding(true));
                }
            }
            return res;
        }
    }

    /// <summary>Aviso breve que no toma el foco; desaparece solo.</summary>
}
