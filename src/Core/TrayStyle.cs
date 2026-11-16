using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RockToolbox.Native
{
    // modern dark look + fade-in for the tray icon menu (instead of the old white Windows context menu)
    internal sealed class TrayColors : ProfessionalColorTable
    {
        private static readonly Color Bg = Color.FromArgb(0x1F, 0x25, 0x2D), Hot = Color.FromArgb(0x2D, 0x37, 0x43), Line = Color.FromArgb(0x31, 0x3B, 0x47);
        public override Color ToolStripDropDownBackground { get { return Bg; } }
        public override Color ImageMarginGradientBegin { get { return Bg; } }
        public override Color ImageMarginGradientMiddle { get { return Bg; } }
        public override Color ImageMarginGradientEnd { get { return Bg; } }
        public override Color MenuBorder { get { return Line; } }
        public override Color MenuItemBorder { get { return Hot; } }
        public override Color MenuItemSelected { get { return Hot; } }
        public override Color MenuItemSelectedGradientBegin { get { return Hot; } }
        public override Color MenuItemSelectedGradientEnd { get { return Hot; } }
        public override Color SeparatorDark { get { return Line; } }
        public override Color SeparatorLight { get { return Line; } }
    }

    internal sealed class TrayRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color Bg = Color.FromArgb(0x1F, 0x25, 0x2D), Hot = Color.FromArgb(0x2D, 0x37, 0x43), Line = Color.FromArgb(0x31, 0x3B, 0x47), Txt = Color.FromArgb(0xE4, 0xE9, 0xF0), Acc = Color.FromArgb(0x4D, 0xA3, 0xC4);
        public TrayRenderer() : base(new TrayColors()) { RoundedEdges = false; }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { using (SolidBrush b = new SolidBrush(Bg)) e.Graphics.FillRectangle(b, e.AffectedBounds); }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Line)) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(4, 1, e.Item.Width - 9, e.Item.Height - 3);
            using (GraphicsPath gp = Round(r, 7)) using (SolidBrush b = new SolidBrush(Hot)) e.Graphics.FillPath(b, gp);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Txt : Color.FromArgb(0x6B, 0x77, 0x86);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (Pen p = new Pen(Line)) e.Graphics.DrawLine(p, 12, e.Item.Height / 2, e.Item.Width - 12, e.Item.Height / 2);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = e.ImageRectangle;
            using (SolidBrush b = new SolidBrush(Acc)) e.Graphics.FillEllipse(b, r.Left + r.Width / 2 - 4, r.Top + r.Height / 2 - 4, 8, 8);
        }

        private static GraphicsPath Round(Rectangle r, int d)
        {
            GraphicsPath p = new GraphicsPath();
            int k = d * 2;
            p.AddArc(r.Left, r.Top, k, k, 180, 90); p.AddArc(r.Right - k, r.Top, k, k, 270, 90);
            p.AddArc(r.Right - k, r.Bottom - k, k, k, 0, 90); p.AddArc(r.Left, r.Bottom - k, k, k, 90, 90);
            p.CloseFigure();
            return p;
        }

        // call once on a ContextMenuStrip
        public static void Apply(ContextMenuStrip m)
        {
            m.Renderer = new TrayRenderer();
            m.ShowImageMargin = false; m.ShowCheckMargin = true;
            m.Font = new Font("Segoe UI", 10f);
            m.BackColor = Bg; m.ForeColor = Txt;
            m.Padding = new Padding(0, 4, 0, 4);
            foreach (ToolStripItem i in m.Items) if (!(i is ToolStripSeparator)) i.Padding = new Padding(6, 6, 6, 6);
            Timer t = new Timer(); t.Interval = 15;
            m.Opening += delegate { m.Opacity = 0.0; };
            m.Opened += delegate { t.Start(); };
            t.Tick += delegate { double o = m.Opacity + 0.18; if (o >= 1.0) { o = 1.0; t.Stop(); } m.Opacity = o; };
            m.Closed += delegate { t.Stop(); };
        }
    }
}
