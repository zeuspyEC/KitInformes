// Simulador de teclado y mouse para las pruebas automáticas del Capturador.
// Uso: Entrada.exe teclas ctrl alt s | clic X Y | arrastrar X1 Y1 X2 Y2 | frente PID | mover PID X Y ANCHO ALTO | marco PID | objetivo
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Entrada
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, int flags, int extra);
    [DllImport("user32.dll")] static extern void mouse_event(int flags, int x, int y, int data, int extra);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);

    static byte Vk(string t)
    {
        switch (t.ToLowerInvariant())
        {
            case "ctrl": return 0x11;
            case "alt": return 0x12;
            case "shift": return 0x10;
            case "esc": return 0x1B;
            case "enter": return 0x0D;
            default: return (byte)char.ToUpperInvariant(t[0]);
        }
    }

    static IntPtr Ventana(string pid)
    {
        using (var p = Process.GetProcessById(int.Parse(pid))) { p.Refresh(); return p.MainWindowHandle; }
    }

    static int Main(string[] a)
    {
        SetProcessDPIAware();
        switch (a[0])
        {
            case "teclas":
                for (int i = 1; i < a.Length; i++) { keybd_event(Vk(a[i]), 0, 0, 0); Thread.Sleep(30); }
                for (int i = a.Length - 1; i >= 1; i--) { keybd_event(Vk(a[i]), 0, 2, 0); Thread.Sleep(30); }
                return 0;
            case "clic":
                SetCursorPos(int.Parse(a[1]), int.Parse(a[2])); Thread.Sleep(150);
                mouse_event(2, 0, 0, 0, 0); Thread.Sleep(60); mouse_event(4, 0, 0, 0, 0);
                return 0;
            case "arrastrar":
            {
                int x1 = int.Parse(a[1]), y1 = int.Parse(a[2]), x2 = int.Parse(a[3]), y2 = int.Parse(a[4]);
                SetCursorPos(x1, y1); Thread.Sleep(150); mouse_event(2, 0, 0, 0, 0);
                for (int i = 1; i <= 10; i++) { SetCursorPos(x1 + (x2 - x1) * i / 10, y1 + (y2 - y1) * i / 10); Thread.Sleep(30); }
                Thread.Sleep(80); mouse_event(4, 0, 0, 0, 0);
                return 0;
            }
            case "frente":
            {
                IntPtr h = Ventana(a[1]);
                ShowWindow(h, 9);
                keybd_event(0x12, 0, 0, 0); SetForegroundWindow(h); keybd_event(0x12, 0, 2, 0);
                Thread.Sleep(300);
                return GetForegroundWindow() == h ? 0 : 1;
            }
            case "mover":
                return MoveWindow(Ventana(a[1]), int.Parse(a[2]), int.Parse(a[3]), int.Parse(a[4]), int.Parse(a[5]), true) ? 0 : 1;
            case "dialogo":
            {
                // Ventana emergente de prueba, como «¿Guardar la contraseña?» de un navegador: «No, gracias» la cierra
                var f = new Form { Text = "Diálogo de prueba", StartPosition = FormStartPosition.Manual, Location = new Point(700, 280),
                    Size = new Size(340, 170), TopMost = true, FormBorderStyle = FormBorderStyle.FixedToolWindow, BackColor = Color.White };
                f.Controls.Add(new Label { Text = "¿Guardar la contraseña?", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 11f) });
                f.Controls.Add(new Button { Text = "Guardar", Location = new Point(20, 70), Size = new Size(120, 34) });
                var no = new Button { Text = "No, gracias", Location = new Point(170, 70), Size = new Size(120, 34) };
                no.Click += (s, e) => f.Close();
                f.Controls.Add(no);
                Application.Run(f);
                return 0;
            }
            case "objetivo":
            {
                // Ventana de prueba en (300,300) de 300×200: botón en (350,350)-(470,390) que cambia con el hover y,
                // al hacer clic, pinta toda la ventana de verde. La zona (520..590, 440..490) no reacciona a nada.
                var f = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = new Point(300, 300), Size = new Size(300, 200), BackColor = Color.White, TopMost = true, Text = "Objetivo de prueba" };
                var b = new Button { Text = "Probar", Location = new Point(50, 50), Size = new Size(120, 40), FlatStyle = FlatStyle.Flat, BackColor = Color.LightGray };
                b.FlatAppearance.MouseOverBackColor = Color.SteelBlue;
                b.Click += (s, e) => { f.BackColor = Color.ForestGreen; };
                f.Controls.Add(b);
                Application.Run(f);
                return 0;
            }
            case "marco":
            {
                RECT r;
                DwmGetWindowAttribute(Ventana(a[1]), 9, out r, 16);
                Console.WriteLine((r.R - r.L) + "x" + (r.B - r.T));
                return 0;
            }
        }
        return 1;
    }
}
