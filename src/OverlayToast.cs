using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DmcKbmMod
{
    // Non-focus-stealing, click-through topmost HUD toast for in-game preset/status feedback.
    public class OverlayToast : Form
    {
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;

        private string _title = "";
        private string _subtitle = "";
        private readonly Timer _hideTimer;

        public OverlayToast()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(24, 24, 32);
            Size = new Size(340, 68);
            Opacity = 0.94;

            _hideTimer = new Timer { Interval = 1800 };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                Hide();
            };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
                return cp;
            }
        }

        public void ShowMessage(string title, string subtitle)
        {
            _title = title ?? "";
            _subtitle = subtitle ?? "";

            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(screen.Right - Width - 22, screen.Top + 22);

            _hideTimer.Stop();
            _hideTimer.Start();

            if (!Visible) Show();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Left crimson accent bar
            using (var accent = new SolidBrush(Color.FromArgb(208, 40, 58)))
                g.FillRectangle(accent, 0, 0, 5, Height);

            // Subtle border
            using (var border = new Pen(Color.FromArgb(70, 70, 88), 1))
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

            using (var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var subFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (var white = new SolidBrush(Color.FromArgb(245, 245, 250)))
            using (var muted = new SolidBrush(Color.FromArgb(180, 185, 200)))
            {
                g.DrawString(_title, titleFont, white, 16, 12);
                g.DrawString(_subtitle, subFont, muted, 16, 36);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _hideTimer != null)
            {
                _hideTimer.Stop();
                _hideTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
