// KitInformes — programa principal (capturador residente)
// Botón flotante persistente + atajos globales para tomar capturas de pantalla
// y registrar su información (fecha, modo, ventana, proceso) para armar informes.
// Compila con el csc de .NET Framework 4.x incluido en Windows (C# 5): ver construir.ps1.
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

[assembly: System.Reflection.AssemblyTitle("KitInformes")]
[assembly: System.Reflection.AssemblyDescription("Capturas para informes con botón flotante, atajos, análisis de lo que se ve y clic verificado")]
[assembly: System.Reflection.AssemblyProduct("KitInformes")]
[assembly: System.Reflection.AssemblyCompany("Erick Costa")]
[assembly: System.Reflection.AssemblyVersion("1.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.2.0.0")]

namespace KitInformes
{
    class SelectorRegion : Form
    {
        readonly Bitmap fondo;
        Point inicio;
        Rectangle sel = Rectangle.Empty;
        bool arrastrando;
        public Bitmap Recorte;

        public SelectorRegion(Bitmap pantalla)
        {
            fondo = pantalla;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "Capturador - seleccionar región";
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; Close(); return; }
            arrastrando = true; inicio = e.Location; sel = new Rectangle(e.Location, Size.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!arrastrando) return;
            sel = Rectangle.FromLTRB(Math.Min(inicio.X, e.X), Math.Min(inicio.Y, e.Y), Math.Max(inicio.X, e.X), Math.Max(inicio.Y, e.Y));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!arrastrando) return;
            arrastrando = false;
            OnMouseMove(e);
            if (sel.Width >= 4 && sel.Height >= 4)
            {
                Recorte = fondo.Clone(sel, fondo.PixelFormat);
                DialogResult = DialogResult.OK;
            }
            else DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(fondo, 0, 0);
            using (var velo = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
            {
                var region = new Region(ClientRectangle);
                if (sel.Width > 0) region.Exclude(sel);
                g.FillRegion(velo, region);
            }
            using (var f = new Font("Segoe UI", 10f))
            {
                string ayuda = "Arrastra para elegir la región · Esc o clic derecho para cancelar";
                SizeF t = g.MeasureString(ayuda, f);
                var caja = new RectangleF((Width - t.Width) / 2 - 10, 16, t.Width + 20, t.Height + 10);
                using (var b = new SolidBrush(Color.FromArgb(220, 31, 41, 55))) g.FillRectangle(b, caja);
                g.DrawString(ayuda, f, Brushes.White, caja.X + 10, caja.Y + 5);
                if (sel.Width > 0)
                {
                    using (var p = new Pen(Color.FromArgb(56, 189, 248), 2)) g.DrawRectangle(p, sel);
                    string dim = sel.Width + " × " + sel.Height;
                    g.DrawString(dim, f, Brushes.White, sel.X + 4, Math.Max(0, sel.Y - 22));
                }
            }
        }
    }

    /// <summary>
    /// Ciclo «actuar y verificar»: captura antes, mueve el cursor o hace clic, captura después y compara
    /// los píxeles para confirmar que algo cambió y dónde. Deja las tres imágenes y una línea en acciones.csv.
    /// </summary>
    class Aviso : Form
    {
        readonly string titulo, texto;
        readonly bool problema;

        public Aviso(string titulo, string texto, bool problema, Rectangle ancla, int ms)
        {
            this.titulo = titulo; this.texto = texto; this.problema = problema;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(31, 41, 55);
            Cursor = Cursors.Hand;
            using (var g = CreateGraphics())
            using (var ft = new Font("Segoe UI Semibold", 9.5f))
            using (var fx = new Font("Segoe UI", 9f))
            {
                SizeF a = g.MeasureString(titulo, ft, 380), b = g.MeasureString(texto, fx, 380);
                Size = new Size((int)Math.Max(a.Width, b.Width) + 28, (int)(a.Height + b.Height) + 22);
            }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Point p = ancla.IsEmpty ? new Point(wa.Right - Width - 16, wa.Bottom - Height - 16)
                                    : new Point(ancla.Left - Width - 10, ancla.Top + (ancla.Height - Height) / 2);
            if (p.X < wa.Left) p.X = ancla.Right + 10;
            p.Y = Math.Max(wa.Top, Math.Min(p.Y, wa.Bottom - Height));
            Location = p;
            var t = new System.Windows.Forms.Timer { Interval = ms };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); Close(); };
            t.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Nativo.WS_EX_TOOLWINDOW | Nativo.WS_EX_TOPMOST | Nativo.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Nativo.WM_MOUSEACTIVATE) { m.Result = (IntPtr)Nativo.MA_NOACTIVATE; return; }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var barra = new SolidBrush(problema ? Color.FromArgb(217, 119, 6) : Dibujo.Ok)) g.FillRectangle(barra, 0, 0, 5, Height);
            using (var ft = new Font("Segoe UI Semibold", 9.5f))
            using (var fx = new Font("Segoe UI", 9f))
            {
                SizeF a = g.MeasureString(titulo, ft, 380);
                g.DrawString(titulo, ft, Brushes.White, new RectangleF(16, 9, 380, a.Height));
                using (var b = new SolidBrush(Color.FromArgb(209, 213, 219)))
                    g.DrawString(texto, fx, b, new RectangleF(16, 11 + a.Height, 380, Height));
            }
        }
    }

    /// <summary>Botón flotante, siempre encima, que no roba el foco a la ventana que se quiere capturar.</summary>
    /// <summary>Ventana de configuración: solo las dos opciones principales (botón visible y modo de uso).</summary>
    class Configuracion : Form
    {
        readonly CheckBox chkBoton;
        readonly RadioButton rbCapturar, rbAnalizar;

        public bool BotonVisible { get { return chkBoton.Checked; } }
        public string Modo { get { return rbAnalizar.Checked ? "analizar" : "capturar"; } }

        public Configuracion(bool botonVisible, string modo)
        {
            Text = "Configuración - KitInformes";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = true;
            Font = new Font("Segoe UI", 9.5f);
            Icon = Dibujo.CrearIcono(32);
            ClientSize = new Size(500, 372);
            BackColor = Color.White;

            var titulo = new Label { Text = "KitInformes", Font = new Font("Segoe UI Semibold", 14f), Location = new Point(20, 14), AutoSize = true };
            var sub = new Label { Text = "Capturas para informes con contexto de lo que se ve en pantalla.", ForeColor = Color.FromArgb(90, 98, 112), Location = new Point(22, 46), AutoSize = true };

            var g1 = new GroupBox { Text = "Botón flotante", Location = new Point(20, 78), Size = new Size(460, 92) };
            chkBoton = new CheckBox { Text = "Mostrar el botón flotante en pantalla", Checked = botonVisible, Location = new Point(16, 26), AutoSize = true };
            var nota1 = new Label { Text = "Si se oculta, los atajos siguen funcionando (Ctrl+Alt+S, V, R) y Ctrl+Alt+B\nlo vuelve a mostrar. El ícono de la bandeja abre esta configuración.",
                ForeColor = Color.FromArgb(90, 98, 112), Location = new Point(34, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            g1.Controls.AddRange(new Control[] { chkBoton, nota1 });

            var g2 = new GroupBox { Text = "Modo de uso", Location = new Point(20, 182), Size = new Size(460, 132) };
            rbCapturar = new RadioButton { Text = "Solo capturar", Checked = modo != "analizar", Location = new Point(16, 26), AutoSize = true };
            var nota2 = new Label { Text = "Guarda la imagen y la anota en capturas.csv.", ForeColor = Color.FromArgb(90, 98, 112),
                Location = new Point(34, 48), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            rbAnalizar = new RadioButton { Text = "Capturar y analizar", Checked = modo == "analizar", Location = new Point(16, 72), AutoSize = true };
            var nota3 = new Label { Text = "Además describe lo que se ve (ventana, textos, botones y campos) en un\narchivo _analisis.md junto a la imagen y muestra un resumen como respuesta.",
                ForeColor = Color.FromArgb(90, 98, 112), Location = new Point(34, 94), AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
            g2.Controls.AddRange(new Control[] { rbCapturar, nota2, rbAnalizar, nota3 });

            // Ventana independiente (no modal): los botones fijan el resultado y la cierran
            var guardar = new Button { Text = "Guardar", Location = new Point(296, 328), Size = new Size(88, 30) };
            var cancelar = new Button { Text = "Cancelar", Location = new Point(392, 328), Size = new Size(88, 30) };
            guardar.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            cancelar.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            AcceptButton = guardar; CancelButton = cancelar;
            Controls.AddRange(new Control[] { titulo, sub, g1, g2, guardar, cancelar });
        }
    }

    class BotonFlotante : Form
    {
        const int TAM = 52;
        const int ID_PANTALLA = 1, ID_VENTANA = 2, ID_REGION = 3, ID_BOTON = 4;

        readonly Config cfg;
        readonly Registro registro;
        readonly NotifyIcon bandeja;
        readonly ContextMenuStrip menu;
        readonly System.Windows.Forms.Timer vigia;
        readonly ToolTip ayuda;
        readonly uint miPid = (uint)Process.GetCurrentProcess().Id;
        IntPtr ultimaAjena = IntPtr.Zero;
        bool hover, listo, ocupado, presionado, moviendo;
        Point origenClic, origenForm;
        readonly List<string> atajosFallidos = new List<string>();
        ToolStripMenuItem itemBoton, itemPortapapeles, itemSonido;

        public BotonFlotante(Config cfg)
        {
            this.cfg = cfg;
            registro = new Registro(cfg.Carpeta);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(TAM, TAM);
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            DoubleBuffered = true;
            Text = "Capturador";

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            if (cfg.X == int.MinValue || !SystemInformation.VirtualScreen.Contains(new Rectangle(cfg.X, cfg.Y, TAM, TAM)))
            { cfg.X = wa.Right - TAM - 16; cfg.Y = wa.Top + wa.Height * 2 / 5; }
            Location = new Point(cfg.X, cfg.Y);

            menu = CrearMenu();
            ayuda = new ToolTip();
            ayuda.SetToolTip(this, "Clic: pantalla completa · Clic derecho: más opciones\nCtrl+Alt+S pantalla · Ctrl+Alt+V ventana · Ctrl+Alt+R región · Ctrl+Alt+B mostrar/ocultar");

            bandeja = new NotifyIcon { Icon = Dibujo.CrearIcono(32), Text = "KitInformes (Ctrl+Alt+S)", ContextMenuStrip = menu, Visible = true };
            bandeja.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) AbrirConfiguracion(); };
            
            Icon = Dibujo.CrearIcono(32);

            vigia = new System.Windows.Forms.Timer { Interval = 250 };
            vigia.Tick += (s, e) => Vigilar();
            vigia.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Nativo.WS_EX_TOOLWINDOW | Nativo.WS_EX_TOPMOST | Nativo.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Registrar(ID_PANTALLA, Keys.S, "Ctrl+Alt+S");
            Registrar(ID_VENTANA, Keys.V, "Ctrl+Alt+V");
            Registrar(ID_REGION, Keys.R, "Ctrl+Alt+R");
            Registrar(ID_BOTON, Keys.B, "Ctrl+Alt+B");
            using (var gp = new GraphicsPath()) { gp.AddEllipse(0, 0, TAM, TAM); Region = new Region(gp); }
        }

        void Registrar(int id, Keys tecla, string nombre)
        {
            if (!Nativo.RegisterHotKey(Handle, id, Nativo.MOD_CONTROL | Nativo.MOD_ALT | Nativo.MOD_NOREPEAT, (uint)tecla))
                atajosFallidos.Add(nombre);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            cfg.Guardar();
            string msg = "Clic en el botón o Ctrl+Alt+S para capturar. Carpeta: " + cfg.Carpeta;
            if (atajosFallidos.Count > 0) msg = "Atajos ocupados por otra aplicación: " + string.Join(", ", atajosFallidos.ToArray()) + ". " + msg;
            Avisar("Capturador listo", msg, atajosFallidos.Count > 0);
            if (!cfg.Visible) Hide();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Nativo.WM_MOUSEACTIVATE) { m.Result = (IntPtr)Nativo.MA_NOACTIVATE; return; }
            if (m.Msg == Nativo.WM_HOTKEY)
            {
                switch (m.WParam.ToInt32())
                {
                    case ID_PANTALLA: Capturar("pantalla"); break;
                    case ID_VENTANA: Capturar("ventana"); break;
                    case ID_REGION: Capturar("region"); break;
                    case ID_BOTON: AlternarBoton(); break;
                }
                return;
            }
            base.WndProc(ref m);
        }

        /// <summary>Recuerda la última ventana activa que no sea del Capturador y mantiene el botón encima.</summary>
        void Vigilar()
        {
            IntPtr h = Nativo.GetForegroundWindow();
            if (EsCapturable(h)) ultimaAjena = h;
            if (Visible) Nativo.SetWindowPos(Handle, Nativo.HWND_TOPMOST, 0, 0, 0, 0, Nativo.SWP_NOMOVE | Nativo.SWP_NOSIZE | Nativo.SWP_NOACTIVATE);
        }

        // Ventanas del sistema que toman el foco de paso (notificaciones, menú Inicio, búsqueda) y no son lo que se quiere capturar
        static readonly HashSet<string> Excluidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "ShellExperienceHost", "StartMenuExperienceHost", "SearchUI", "SearchApp", "SearchHost", "LockApp" };
        readonly Dictionary<uint, string> nombres = new Dictionary<uint, string>();

        bool EsCapturable(IntPtr h)
        {
            if (h == IntPtr.Zero) return false;
            uint pid;
            Nativo.GetWindowThreadProcessId(h, out pid);
            if (pid == miPid) return false;
            string nombre;
            if (!nombres.TryGetValue(pid, out nombre))
            {
                try { using (var p = Process.GetProcessById((int)pid)) nombre = p.ProcessName; } catch (ArgumentException) { nombre = ""; } catch (InvalidOperationException) { nombre = ""; }
                nombres[pid] = nombre;
            }
            if (Excluidos.Contains(nombre)) return false;
            var sb = new StringBuilder(4);
            return Nativo.GetWindowText(h, sb, sb.Capacity) > 0; // la barra de tareas y otras ventanas sin título no cuentan
        }

        IntPtr VentanaObjetivo()
        {
            IntPtr h = Nativo.GetForegroundWindow();
            return EsCapturable(h) ? h : ultimaAjena;
        }

        Aviso aviso;
        /// <summary>Aviso propio junto al botón: no roba el foco ni se acumula en el centro de notificaciones.</summary>
        void Avisar(string titulo, string texto, bool problema) { Avisar(titulo, texto, problema, problema ? 4500 : 1800); }

        void Avisar(string titulo, string texto, bool problema, int ms)
        {
            if (aviso != null && !aviso.IsDisposed) aviso.Close();
            aviso = new Aviso(titulo, texto, problema, Visible ? Bounds : Rectangle.Empty, ms);
            aviso.Click += (s, e) => AbrirCarpeta();
            aviso.Show();
        }

        public void Capturar(string modo)
        {
            if (ocupado) return;
            ocupado = true;
            try
            {
                var info = InfoVentana.De(VentanaObjetivo());
                if (aviso != null && !aviso.IsDisposed) aviso.Close();
                // Pantalla limpia: se cierran ventanas emergentes («Guardar contraseña» → «Nunca», «No, gracias»…)
                List<string> cerrados = cfg.Limpiar && modo != "region" ? Limpieza.Descartar(info.Handle, cfg.Descartar) : new List<string>();
                // Si la ventana emergente era la activa, ya no existe: se captura y analiza la que quedó debajo
                if (cerrados.Count > 0 && !Nativo.IsWindowVisible(info.Handle)) { Thread.Sleep(300); info = InfoVentana.De(VentanaObjetivo()); }
                bool estabaVisible = Visible;
                if (estabaVisible) { Hide(); Application.DoEvents(); Thread.Sleep(150); }
                Bitmap bmp = null;
                try
                {
                    if (modo == "pantalla") bmp = Captura.Pantalla();
                    else if (modo == "ventana") bmp = Captura.Ventana(info.Handle);
                    else if (modo == "region")
                        using (var pantalla = Captura.Pantalla())
                        using (var sel = new SelectorRegion(pantalla))
                            if (sel.ShowDialog() == DialogResult.OK) bmp = sel.Recorte;
                }
                finally { if (estabaVisible) Show(); }

                if (bmp == null)
                {
                    if (modo == "ventana") Avisar("Capturador", "No hay una ventana activa para capturar.", true);
                    return;
                }
                using (bmp)
                {
                    string ruta = registro.Guardar(bmp, modo, info);
                    if (cfg.Portapapeles) CopiarAlPortapapeles(bmp);
                    if (cfg.Sonido) System.Media.SystemSounds.Asterisk.Play();
                    EjecutarDespues(ruta);
                    Destello();
                    string limpio = cerrados.Count > 0 ? "\nSe cerró: «" + string.Join("», «", cerrados.ToArray()) + "»" : "";
                    if (cfg.Analizar && info.Handle != IntPtr.Zero)
                    {
                        // Modo «Capturar y analizar»: contexto de lo que se ve, guardado junto a la imagen
                        var r = Analizador.Analizar(info.Handle, ruta);
                        Avisar("Captura analizada · " + Path.GetFileName(ruta), r.Resumen + limpio, false, 7000);
                    }
                    else
                    {
                        string detalle = info.Titulo.Length > 0 && modo != "region" ? "\n" + info.Titulo : "";
                        Avisar("Captura guardada", Path.GetFileName(ruta) + detalle + limpio, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Avisar("Error al capturar", ex.Message, true);
            }
            finally { ocupado = false; }
        }

        /// <summary>Lanza el comando configurado en «despues=» sin esperar a que termine.</summary>
        void EjecutarDespues(string ruta)
        {
            if (string.IsNullOrEmpty(cfg.Despues)) return;
            try
            {
                string cmd = cfg.Despues.Replace("{archivo}", "\"" + ruta + "\"");
                Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + cmd + "\"") { CreateNoWindow = true, UseShellExecute = false, WorkingDirectory = cfg.Carpeta });
            }
            catch (Exception ex) { Avisar("Comando «despues» falló", ex.Message, true); }
        }

        static void CopiarAlPortapapeles(Bitmap bmp)
        {
            for (int i = 0; i < 5; i++)
            {
                try { Clipboard.SetImage(bmp); return; }
                catch (ExternalException) { Thread.Sleep(100); }
            }
        }

        void Destello()
        {
            listo = true; Invalidate();
            var t = new System.Windows.Forms.Timer { Interval = 700 };
            t.Tick += (s, e) => { listo = false; Invalidate(); t.Stop(); t.Dispose(); };
            t.Start();
        }

        void AlternarBoton()
        {
            cfg.Visible = !Visible;
            if (cfg.Visible) Show(); else Hide();
            itemBoton.Checked = cfg.Visible;
            cfg.Guardar();
        }

        void AbrirCarpeta()
        {
            Directory.CreateDirectory(cfg.Carpeta);
            Process.Start("explorer.exe", "\"" + cfg.Carpeta + "\"");
        }

        ContextMenuStrip CrearMenu()
        {
            var m = new ContextMenuStrip();
            var conf = new ToolStripMenuItem("Configuración…", null, (s, e) => AbrirConfiguracion()) { Font = new Font(m.Font, FontStyle.Bold) };
            m.Items.Add(conf);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Capturar pantalla completa\tCtrl+Alt+S", null, (s, e) => Diferido("pantalla"));
            m.Items.Add("Capturar ventana activa\tCtrl+Alt+V", null, (s, e) => Diferido("ventana"));
            m.Items.Add("Capturar región…\tCtrl+Alt+R", null, (s, e) => Diferido("region"));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Abrir carpeta de capturas", null, (s, e) => AbrirCarpeta());
            m.Items.Add("Abrir registro (capturas.csv)", null, (s, e) =>
            {
                string csv = Path.Combine(cfg.Carpeta, "capturas.csv");
                if (File.Exists(csv)) Process.Start(csv); else AbrirCarpeta();
            });
            m.Items.Add("Cambiar carpeta…", null, (s, e) => CambiarCarpeta());
            m.Items.Add(new ToolStripSeparator());
            itemBoton = new ToolStripMenuItem("Mostrar botón flotante\tCtrl+Alt+B", null, (s, e) => AlternarBoton()) { Checked = cfg.Visible };
            itemPortapapeles = new ToolStripMenuItem("Copiar también al portapapeles", null, (s, e) =>
            { cfg.Portapapeles = !cfg.Portapapeles; itemPortapapeles.Checked = cfg.Portapapeles; cfg.Guardar(); }) { Checked = cfg.Portapapeles };
            itemSonido = new ToolStripMenuItem("Sonido al capturar", null, (s, e) =>
            { cfg.Sonido = !cfg.Sonido; itemSonido.Checked = cfg.Sonido; cfg.Guardar(); }) { Checked = cfg.Sonido };
            m.Items.Add(itemBoton);
            m.Items.Add(itemPortapapeles);
            m.Items.Add(itemSonido);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Salir", null, (s, e) => Close());
            return m;
        }

        /// <summary>Espera a que el menú se cierre antes de capturar, para que no salga en la imagen.</summary>
        void Diferido(string modo)
        {
            var t = new System.Windows.Forms.Timer { Interval = 250 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); Capturar(modo); };
            t.Start();
        }

        void CambiarCarpeta()
        {
            using (var d = new FolderBrowserDialog { Description = "Carpeta donde se guardan las capturas", SelectedPath = cfg.Carpeta })
                if (d.ShowDialog() == DialogResult.OK)
                {
                    cfg.Carpeta = d.SelectedPath;
                    registro.Carpeta = d.SelectedPath;
                    cfg.Guardar();
                }
        }

        Configuracion ventanaConfig;
        public void AbrirConfiguracion()
        {
            if (ventanaConfig != null && !ventanaConfig.IsDisposed) { ventanaConfig.Activate(); return; }
            // Ventana independiente (no modal y sin dueño): así no depende del aviso de inicio ni del botón oculto
            var v = new Configuracion(Visible, cfg.Modo);
            v.FormClosed += (s, e) =>
            {
                if (v.DialogResult == DialogResult.OK)
                {
                    cfg.Modo = v.Modo;
                    if (v.BotonVisible != Visible) AlternarBoton();
                    cfg.Guardar();
                    Avisar("Configuración guardada", (cfg.Analizar ? "Modo: capturar y analizar." : "Modo: solo capturar.") +
                        (Visible ? "" : " Botón oculto (Ctrl+Alt+B lo muestra)."), false);
                }
                v.Dispose();
            };
            ventanaConfig = v;
            v.Show();
            v.Activate();
        }

        public void MostrarAviso()
        {
            if (!Visible) AlternarBoton();
            Avisar("Capturador", "Ya está en ejecución. Carpeta: " + cfg.Carpeta, false);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { presionado = true; moviendo = false; origenClic = Cursor.Position; origenForm = Location; }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (presionado)
            {
                Point c = Cursor.Position;
                int dx = c.X - origenClic.X, dy = c.Y - origenClic.Y;
                if (moviendo || Math.Abs(dx) + Math.Abs(dy) > 5) { moviendo = true; Location = new Point(origenForm.X + dx, origenForm.Y + dy); }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && presionado)
            {
                presionado = false;
                if (moviendo) { cfg.X = Left; cfg.Y = Top; cfg.Guardar(); }
                else Capturar("pantalla");
            }
            else if (e.Button == MouseButtons.Right) menu.Show(Cursor.Position);
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var bmp = new Bitmap(TAM, TAM, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) Dibujo.Icono(g, TAM, listo ? Dibujo.Ok : hover ? Dibujo.Hover : Dibujo.Fondo, listo);
                e.Graphics.Clear(Color.Magenta);
                e.Graphics.DrawImageUnscaled(bmp, 0, 0);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            for (int id = ID_PANTALLA; id <= ID_BOTON; id++) Nativo.UnregisterHotKey(Handle, id);
            cfg.X = Left; cfg.Y = Top;
            cfg.Guardar();
            vigia.Stop();
            bandeja.Visible = false;
            bandeja.Dispose();
            base.OnFormClosing(e);
        }
    }

    static class Programa
    {
        const string MUTEX = @"Local\KitInformes.Capturador";
        const string EV_SALIR = @"Local\KitInformes.Capturador.Salir";
        const string EV_MOSTRAR = @"Local\KitInformes.Capturador.Mostrar";
        const string EV_CONFIG = @"Local\KitInformes.Capturador.Configuracion";

        [STAThread]
        static int Main(string[] args)
        {
            Nativo.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string dirBase = Path.GetDirectoryName(Application.ExecutablePath);
            var lista = new List<string>(args);

            int i = lista.IndexOf("--exportar-icono");
            if (i >= 0 && i + 1 < lista.Count) { Dibujo.ExportarIco(lista[i + 1]); return 0; }

            // Comandos de terminal: analizar, limpiar, elementos, clic-nombre, explorar, guion, ayuda
            if (lista.Count > 0 && Array.IndexOf(Comandos.Nombres, lista[0].TrimStart('-').ToLowerInvariant()) >= 0)
                return Comandos.Ejecutar(lista, dirBase);

            // Captura única desde la línea de comandos: Capturador.exe --capturar pantalla|ventana
            // Diagnóstico: devuelve cuántos atajos Ctrl+Alt+S/V/R/B están ocupados por otra aplicación
            if (lista.Contains("--probar-atajos"))
            {
                int ocupados = 0;
                using (var f = new Form())
                {
                    IntPtr h = f.Handle;
                    Keys[] teclas = { Keys.S, Keys.V, Keys.R, Keys.B };
                    for (int k = 0; k < teclas.Length; k++)
                    {
                        if (Nativo.RegisterHotKey(h, 100 + k, Nativo.MOD_CONTROL | Nativo.MOD_ALT | Nativo.MOD_NOREPEAT, (uint)teclas[k])) Nativo.UnregisterHotKey(h, 100 + k);
                        else ocupados++;
                    }
                }
                return ocupados;
            }

            // Actuar y verificar: Capturador.exe --sondear X Y | --clic X Y  [--esperar ms]
            // Código de salida: 0 = hubo cambio en pantalla, 1 = no cambió nada, 3 = argumentos inválidos
            foreach (string modoAccion in new[] { "--sondear", "--clic" })
            {
                i = lista.IndexOf(modoAccion);
                if (i < 0) continue;
                int ax, ay, espera = modoAccion == "--clic" ? 1200 : 600;
                if (i + 2 >= lista.Count || !int.TryParse(lista[i + 1], out ax) || !int.TryParse(lista[i + 2], out ay)) return 3;
                int j = lista.IndexOf("--esperar");
                if (j >= 0 && j + 1 < lista.Count) int.TryParse(lista[j + 1], out espera);
                var r = Accion.Ejecutar(modoAccion.Substring(2), ax, ay, espera, Config.Cargar(dirBase).Carpeta);
                return r.Cambio ? 0 : 1;
            }

            i = lista.IndexOf("--capturar");
            if (i >= 0)
            {
                string modo = i + 1 < lista.Count ? lista[i + 1] : "pantalla";
                var cfg = Config.Cargar(dirBase);
                var info = InfoVentana.De(Nativo.GetForegroundWindow());
                Bitmap bmp = modo == "ventana" ? Captura.Ventana(info.Handle) : Captura.Pantalla();
                if (bmp == null) return 2;
                using (bmp) new Registro(cfg.Carpeta).Guardar(bmp, modo == "ventana" ? "ventana" : "pantalla", info);
                return 0;
            }

            bool nuevo;
            using (var mutex = new Mutex(true, MUTEX, out nuevo))
            {
                if (!nuevo)
                {
                    string ev = lista.Contains("--salir") ? EV_SALIR : lista.Contains("--configuracion") ? EV_CONFIG : EV_MOSTRAR;
                    EventWaitHandle h;
                    if (EventWaitHandle.TryOpenExisting(ev, out h)) using (h) h.Set();
                    return 0;
                }
                if (lista.Contains("--salir")) return 0;

                using (var salir = new EventWaitHandle(false, EventResetMode.AutoReset, EV_SALIR))
                using (var mostrar = new EventWaitHandle(false, EventResetMode.AutoReset, EV_MOSTRAR))
                using (var config = new EventWaitHandle(false, EventResetMode.AutoReset, EV_CONFIG))
                {
                    var form = new BotonFlotante(Config.Cargar(dirBase));
                    if (lista.Contains("--configuracion")) form.Shown += (s, e) => form.BeginInvoke((Action)(() => form.AbrirConfiguracion()));
                    var hilo = new Thread(() =>
                    {
                        var hs = new WaitHandle[] { salir, mostrar, config };
                        while (true)
                        {
                            int n = WaitHandle.WaitAny(hs);
                            try
                            {
                                if (n == 0) { form.BeginInvoke((Action)(() => form.Close())); return; }
                                if (n == 2) form.BeginInvoke((Action)(() => form.AbrirConfiguracion()));
                                else form.BeginInvoke((Action)(() => form.MostrarAviso()));
                            }
                            catch (InvalidOperationException) { return; }
                        }
                    }) { IsBackground = true };
                    hilo.Start();
                    Application.Run(form);
                }
            }
            return 0;
        }
    }
}
