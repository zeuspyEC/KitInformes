// Lectura de la pantalla SIN IA: elementos interactivos por UI Automation (accesibilidad de Windows)
// y exploración puramente visual por hover en cuadrícula.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace KitInformes
{
    class Elemento
    {
        public int Numero;
        public string Tipo = "", Nombre = "";
        public Rectangle Caja;
        public bool Habilitado;
        public Point Centro { get { return new Point(Caja.Left + Caja.Width / 2, Caja.Top + Caja.Height / 2); } }
    }

    static class Elementos
    {
        static readonly ControlType[] Interactivos = {
            ControlType.Button, ControlType.Hyperlink, ControlType.Edit, ControlType.CheckBox, ControlType.RadioButton,
            ControlType.ComboBox, ControlType.MenuItem, ControlType.TabItem, ControlType.ListItem, ControlType.SplitButton,
            ControlType.Slider, ControlType.TreeItem, ControlType.DataItem };

        static readonly Dictionary<ControlType, string> Nombres = new Dictionary<ControlType, string> {
            { ControlType.Button, "botón" }, { ControlType.Hyperlink, "enlace" }, { ControlType.Edit, "campo" },
            { ControlType.CheckBox, "casilla" }, { ControlType.RadioButton, "opción" }, { ControlType.ComboBox, "lista desplegable" },
            { ControlType.MenuItem, "menú" }, { ControlType.TabItem, "pestaña" }, { ControlType.ListItem, "elemento de lista" },
            { ControlType.SplitButton, "botón" }, { ControlType.Slider, "control deslizante" }, { ControlType.TreeItem, "nodo" },
            { ControlType.DataItem, "fila" } };

        /// <summary>Elementos interactivos visibles de una ventana, ordenados de arriba abajo y de izquierda a derecha.</summary>
        public static List<Elemento> Listar(IntPtr hwnd)
        {
            var lista = new List<Elemento>();
            if (hwnd == IntPtr.Zero) return lista;
            AutomationElement raiz = AutomationElement.FromHandle(hwnd);
            var conds = new Condition[Interactivos.Length];
            for (int i = 0; i < Interactivos.Length; i++) conds[i] = new PropertyCondition(AutomationElement.ControlTypeProperty, Interactivos[i]);
            var cond = new OrCondition(conds);
            Rectangle marco = Captura.MarcoVentana(hwnd);

            // Chrome/Brave/Edge activan su árbol de accesibilidad al primer pedido: se reintenta si viene casi vacío
            AutomationElementCollection col = null;
            for (int intento = 0; intento < 4; intento++)
            {
                col = raiz.FindAll(TreeScope.Descendants, cond);
                if (col.Count > 3) break;
                Thread.Sleep(700);
            }
            foreach (AutomationElement ae in col)
            {
                try
                {
                    var c = ae.Current;
                    if (c.IsOffscreen) continue;
                    System.Windows.Rect r = c.BoundingRectangle;
                    if (r.IsEmpty || r.Width < 4 || r.Height < 4) continue;
                    var caja = new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                    if (!marco.IntersectsWith(caja)) continue;
                    string tipo;
                    if (!Nombres.TryGetValue(c.ControlType, out tipo)) tipo = c.LocalizedControlType;
                    lista.Add(new Elemento { Tipo = tipo, Nombre = (c.Name ?? "").Trim(), Caja = Rectangle.Intersect(caja, marco), Habilitado = c.IsEnabled });
                }
                catch (ElementNotAvailableException) { }
            }
            lista.Sort((a, b) => Math.Abs(a.Caja.Top - b.Caja.Top) > 8 ? a.Caja.Top.CompareTo(b.Caja.Top) : a.Caja.Left.CompareTo(b.Caja.Left));
            for (int i = 0; i < lista.Count; i++) lista[i].Numero = i + 1;
            return lista;
        }

        static string Normalizar(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char ch in s) if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString();
        }

        /// <summary>Busca por nombre (sin distinguir mayúsculas ni tildes): primero coincidencia exacta, luego parcial. Tipo opcional.</summary>
        public static Elemento Buscar(List<Elemento> lista, string nombre, string tipo)
        {
            string n = Normalizar(nombre), t = Normalizar(tipo);
            Elemento parcial = null;
            foreach (var e in lista)
            {
                if (!e.Habilitado) continue;
                if (t.Length > 0 && Normalizar(e.Tipo) != t) continue;
                string en = Normalizar(e.Nombre);
                if (en == n) return e;
                if (parcial == null && n.Length > 0 && en.Contains(n)) parcial = e;
            }
            return parcial;
        }

        /// <summary>Guarda la captura de la ventana con cada elemento recuadrado y numerado, y su lista en CSV.</summary>
        public static string Documentar(IntPtr hwnd, List<Elemento> lista, string carpeta, out string csvRuta)
        {
            Directory.CreateDirectory(carpeta);
            Rectangle marco = Rectangle.Intersect(Captura.MarcoVentana(hwnd), SystemInformation.VirtualScreen);
            int n = new Registro(carpeta).SiguienteNumero();
            string baseNombre = string.Format("{0:000}_{1:yyyy-MM-dd_HH-mm-ss}_elementos", n, DateTime.Now);
            string png = Path.Combine(carpeta, baseNombre + ".png");
            csvRuta = Path.Combine(carpeta, baseNombre + ".csv");
            using (Bitmap bmp = Captura.Area(marco))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var f = new Font("Segoe UI", 8f, FontStyle.Bold))
                using (var pen = new Pen(Color.FromArgb(230, 220, 38, 38), 2))
                using (var fondo = new SolidBrush(Color.FromArgb(230, 220, 38, 38)))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    foreach (var e in lista)
                    {
                        var r = new Rectangle(e.Caja.X - marco.X, e.Caja.Y - marco.Y, e.Caja.Width, e.Caja.Height);
                        g.DrawRectangle(pen, r);
                        string num = e.Numero.ToString();
                        SizeF t = g.MeasureString(num, f);
                        var et = new RectangleF(r.X, Math.Max(0, r.Y - t.Height), t.Width + 2, t.Height);
                        g.FillRectangle(fondo, et);
                        g.DrawString(num, f, Brushes.White, et.X + 1, et.Y);
                    }
                }
                bmp.Save(png, ImageFormat.Png);
            }
            var sb = new StringBuilder("numero;tipo;nombre;x;y;ancho;alto;centro_x;centro_y;habilitado\r\n");
            foreach (var e in lista)
                sb.AppendLine(string.Join(";", new[] { e.Numero.ToString(), e.Tipo, "\"" + e.Nombre.Replace("\"", "\"\"").Replace("\n", " ") + "\"",
                    e.Caja.X.ToString(), e.Caja.Y.ToString(), e.Caja.Width.ToString(), e.Caja.Height.ToString(),
                    e.Centro.X.ToString(), e.Centro.Y.ToString(), e.Habilitado ? "si" : "no" }));
            File.WriteAllText(csvRuta, sb.ToString(), new UTF8Encoding(true));
            return png;
        }

        // ---------- Exploración visual (sin accesibilidad ni IA) ----------
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);

        /// <summary>
        /// Recorre una cuadrícula dentro de «area» pasando el cursor por cada punto y compara un recuadro alrededor
        /// con el estado en reposo: los puntos que cambian son zonas interactivas (hover). Une los puntos vecinos en cajas.
        /// </summary>
        public static List<Rectangle> Explorar(Rectangle area, int paso, string carpeta, out string marcado)
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            area = Rectangle.Intersect(area, vs);
            SetCursorPos(vs.Left + 1, vs.Top + 1);
            Thread.Sleep(400);
            var activos = new List<Point>();
            using (Bitmap reposo = Captura.Area(area))
            {
                for (int y = area.Top + paso / 2; y < area.Bottom; y += paso)
                    for (int x = area.Left + paso / 2; x < area.Right; x += paso)
                    {
                        SetCursorPos(x, y);
                        Thread.Sleep(140);
                        using (Bitmap ahora = Captura.Area(area))
                        {
                            Rectangle z;
                            var local = new Rectangle(x - area.Left - paso, y - area.Top - paso, paso * 2, paso * 2);
                            double pct = Accion.Comparar(reposo, ahora, local, out z);
                            if (pct * (paso * 2) * (paso * 2) / 100.0 >= 4) activos.Add(new Point(x, y));
                        }
                    }
                SetCursorPos(vs.Left + 1, vs.Top + 1);

                // Unir puntos vecinos (distancia de un paso) en cajas
                var cajas = new List<Rectangle>();
                var usado = new bool[activos.Count];
                for (int i = 0; i < activos.Count; i++)
                {
                    if (usado[i]) continue;
                    var pila = new Stack<int>(); pila.Push(i); usado[i] = true;
                    int minX = activos[i].X, minY = activos[i].Y, maxX = minX, maxY = minY;
                    while (pila.Count > 0)
                    {
                        Point p = activos[pila.Pop()];
                        minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X); minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                        for (int j = 0; j < activos.Count; j++)
                            if (!usado[j] && Math.Abs(activos[j].X - p.X) <= paso && Math.Abs(activos[j].Y - p.Y) <= paso) { usado[j] = true; pila.Push(j); }
                    }
                    cajas.Add(Rectangle.FromLTRB(minX - paso / 2, minY - paso / 2, maxX + paso / 2, maxY + paso / 2));
                }

                Directory.CreateDirectory(carpeta);
                int n = new Registro(carpeta).SiguienteNumero();
                marcado = Path.Combine(carpeta, string.Format("{0:000}_{1:yyyy-MM-dd_HH-mm-ss}_explorar.png", n, DateTime.Now));
                using (var m = new Bitmap(reposo))
                {
                    using (var g = Graphics.FromImage(m))
                    using (var pen = new Pen(Color.FromArgb(56, 189, 248), 2))
                    using (var punto = new SolidBrush(Color.FromArgb(200, 220, 38, 38)))
                    {
                        foreach (var p in activos) g.FillEllipse(punto, p.X - area.Left - 3, p.Y - area.Top - 3, 6, 6);
                        foreach (var c in cajas) g.DrawRectangle(pen, c.X - area.Left, c.Y - area.Top, c.Width, c.Height);
                    }
                    m.Save(marcado, ImageFormat.Png);
                }
                return cajas;
            }
        }
    }
}
