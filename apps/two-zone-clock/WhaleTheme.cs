using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace TwoZoneClock {
    internal static class WhaleTheme {
        internal static readonly Color Surface = Color.FromArgb(247, 249, 255);
        internal static readonly Color Border = Color.FromArgb(195, 210, 237);
        internal static readonly Color Ink = Color.FromArgb(40, 58, 99);
        internal static readonly Color Muted = Color.FromArgb(98, 116, 151);
        internal static readonly Color Blue = Color.FromArgb(74, 113, 188);
        internal static readonly Color Day = Color.FromArgb(255, 248, 231);
        internal static readonly Color Night = Color.FromArgb(239, 240, 255);
        private static readonly Image Mascot = LoadMascot();

        private static Image LoadMascot() {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TwoZoneClock.Whalegirl.png")) {
                if (stream == null) throw new InvalidOperationException("The embedded whalegirl artwork is missing.");
                using (var image = Image.FromStream(stream)) return new Bitmap(image);
            }
        }
        internal static Icon CreateIcon() {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TwoZoneClock.Whalegirl.ico")) {
                if (stream == null) throw new InvalidOperationException("The embedded whalegirl icon is missing.");
                using (var icon = new Icon(stream)) return (Icon)icon.Clone();
            }
        }
        internal static void DrawMascot(Graphics graphics, RectangleF bounds) {
            var state = graphics.Save();
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float ratio = Math.Min(bounds.Width / Mascot.Width, bounds.Height / Mascot.Height);
            float width = Mascot.Width * ratio, height = Mascot.Height * ratio;
            graphics.DrawImage(Mascot, bounds.X + (bounds.Width-width)/2, bounds.Y + (bounds.Height-height)/2, width, height);
            graphics.Restore(state);
        }
        internal static GraphicsPath Rounded(RectangleF bounds, float radius) {
            var path = new GraphicsPath(); float diameter = radius*2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right-diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right-diameter, bounds.Bottom-diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom-diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }
        internal static void DrawCard(Graphics graphics, RectangleF bounds, Color color, float radius) {
            using (var path = Rounded(bounds, radius))
            using (var fill = new SolidBrush(color))
            using (var outline = new Pen(Border, 1)) { graphics.FillPath(fill, path); graphics.DrawPath(outline, path); }
        }
        internal static void StyleButton(Button button, bool primary) {
            button.BackColor = primary ? Blue : Color.White;
            button.ForeColor = primary ? Color.White : Ink;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(63, 100, 172) : Color.FromArgb(233, 241, 255);
            button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(52, 86, 153) : Color.FromArgb(217, 231, 252);
            button.Resize += delegate {
                using (var path = Rounded(new RectangleF(0, 0, button.Width, button.Height), 8)) {
                    var old = button.Region; button.Region = new Region(path); if (old != null) old.Dispose();
                }
            };
            using (var path = Rounded(new RectangleF(0, 0, button.Width, button.Height), 8)) button.Region = new Region(path);
        }
        internal static void StyleMenu(ContextMenuStrip menu) {
            menu.BackColor = Surface; menu.ForeColor = Ink;
            menu.Font = new Font("Microsoft YaHei UI", 9f);
            menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors());
            menu.ShowImageMargin = false; menu.ShowCheckMargin = true;
        }
        private sealed class MenuColors : ProfessionalColorTable {
            public override Color ToolStripDropDownBackground { get { return Surface; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color MenuItemSelected { get { return Color.FromArgb(224, 236, 255); } }
            public override Color MenuItemBorder { get { return Border; } }
            public override Color MenuBorder { get { return Border; } }
            public override Color MenuItemSelectedGradientBegin { get { return MenuItemSelected; } }
            public override Color MenuItemSelectedGradientEnd { get { return MenuItemSelected; } }
            public override Color CheckBackground { get { return Color.FromArgb(214, 228, 252); } }
        }
    }
}
