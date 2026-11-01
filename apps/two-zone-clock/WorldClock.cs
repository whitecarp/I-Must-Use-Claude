using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

[assembly: System.Reflection.AssemblyVersion("0.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.3.0.0")]

namespace TwoZoneClock {
    public sealed class Preferences {
        // Keep reading the old setting so upgrades preserve the single-column preference.
        public bool ShowSingapore = true;
        public bool ShouldSerializeShowSingapore() { return false; }
        public bool ShowSource { get { return ShowSingapore; } set { ShowSingapore = value; } }
        public string SourceTimeZoneId = "Singapore Standard Time";
        public string TargetTimeZoneId = "Eastern Standard Time";
        public bool English = false;
        public bool TwentyFourHours = false;
        public bool ShowSeconds = false;
        public bool AlwaysOnTop = true;
        public bool Locked = false;
        public bool HasPosition = false;
        public int X, Y;
        public void Normalize() {
            if (!ClockTime.IsValidZone(SourceTimeZoneId)) SourceTimeZoneId = "Singapore Standard Time";
            if (!ClockTime.IsValidZone(TargetTimeZoneId)) TargetTimeZoneId = "Eastern Standard Time";
        }
    }

    public sealed class ClockReading {
        public DateTimeOffset Local;
        public string Time, Date, Zone;
    }

    public static class ClockTime {
        public static bool IsValidZone(string id) {
            if (id == "local") return true;
            if (String.IsNullOrWhiteSpace(id)) return false;
            try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
            catch (TimeZoneNotFoundException) { return false; }
            catch (InvalidTimeZoneException) { return false; }
        }
        public static TimeZoneInfo ResolveZone(string id) {
            if (id == "local" || String.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Local;
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
            catch (InvalidTimeZoneException) { return TimeZoneInfo.Local; }
        }
        public static ClockReading Read(DateTimeOffset instant, bool singapore, bool use24, bool seconds) {
            return Read(instant, singapore ? "Singapore Standard Time" : "Eastern Standard Time", use24, seconds);
        }
        public static ClockReading Read(DateTimeOffset instant, string zoneId, bool use24, bool seconds) {
            return Read(instant, zoneId, use24, seconds, false);
        }
        public static ClockReading Read(DateTimeOffset instant, string zoneId, bool use24, bool seconds, bool english) {
            var zone = ResolveZone(zoneId);
            var local = TimeZoneInfo.ConvertTime(instant, zone);
            string format = use24 ? (seconds ? "HH:mm:ss" : "HH:mm") : (seconds ? "h:mm:ss tt" : "h:mm tt");
            string[] days = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            return new ClockReading {
                Local = local, Time = local.ToString(format, CultureInfo.InvariantCulture),
                Date = local.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) + "  " + (english ? local.ToString("ddd", CultureInfo.InvariantCulture) : days[(int)local.DayOfWeek]),
                Zone = ZoneLabel(zone, instant, local.Offset)
            };
        }
        public static string OffsetLabel(TimeSpan offset) {
            return "UTC" + (offset < TimeSpan.Zero ? "−" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }
        public static string DifferenceLabel(DateTimeOffset instant, string sourceZoneId, string targetZoneId, bool english) {
            var source = TimeZoneInfo.ConvertTime(instant, ResolveZone(sourceZoneId));
            var target = TimeZoneInfo.ConvertTime(instant, ResolveZone(targetZoneId));
            // Subtract the offsets, not the DateTimeOffsets: both readings represent the same instant.
            var difference = target.Offset - source.Offset;
            int minutes = (int)Math.Abs(difference.TotalMinutes);
            string duration = minutes / 60 > 0 ? (minutes / 60).ToString(CultureInfo.InvariantCulture) + (english ? "h" : "小时") : "";
            if (minutes % 60 > 0) duration += (english && duration.Length > 0 ? " " : "") + (minutes % 60).ToString(CultureInfo.InvariantCulture) + (english ? "m" : "分");
            string relative = minutes == 0 ? (english ? "To: same time" : "目标与原本同一时间")
                : english ? "To: " + duration + (difference > TimeSpan.Zero ? " ahead" : " behind")
                : "目标" + (difference > TimeSpan.Zero ? "快 " : "慢 ") + duration;
            int days = (target.Date - source.Date).Days;
            string day;
            switch (days) {
                case -1: day = english ? "yesterday" : "昨天"; break;
                case 0: day = english ? "same day" : "同一天"; break;
                case 1: day = english ? "tomorrow" : "明天"; break;
                default: day = english ? Math.Abs(days).ToString(CultureInfo.InvariantCulture) + (days < 0 ? " days earlier" : " days later")
                    : (days < 0 ? "早 " : "晚 ") + Math.Abs(days).ToString(CultureInfo.InvariantCulture) + " 天"; break;
            }
            return relative + " · " + day;
        }
        private static string ZoneLabel(TimeZoneInfo zone, DateTimeOffset instant, TimeSpan offset) {
            bool daylight = zone.IsDaylightSavingTime(instant);
            switch (zone.Id) {
                case "Singapore Standard Time": return "SGT";
                case "China Standard Time": return "UTC+08:00";
                case "Eastern Standard Time": return daylight ? "EDT" : "EST";
                case "Central Standard Time": return daylight ? "CDT" : "CST";
                case "Mountain Standard Time": return daylight ? "MDT" : "MST";
                case "Pacific Standard Time": return daylight ? "PDT" : "PST";
                default: return OffsetLabel(offset);
            }
        }
    }

    internal sealed class ClockForm : Form {
        private readonly Preferences prefs;
        private readonly System.Windows.Forms.Timer timer;
        private readonly EventWaitHandle showSignal;
        private readonly bool preview;
        private readonly NotifyIcon tray;
        private readonly Icon clockIcon;
        private float scale = 1;
        private bool dragging;
        private Point dragCursor, dragPosition;
        private string lastStamp = "";
        private DateTimeOffset now;
        private ContextMenuStrip menu;
        private ToolStripMenuItem sourceItem, formatItem, secondsItem, topItem, lockItem;
        private static readonly Color Surface = WhaleTheme.Surface;
        private static readonly Color Border = WhaleTheme.Border;
        private static readonly Color Ink = WhaleTheme.Ink;
        private static readonly Color Muted = WhaleTheme.Muted;
        private static string SettingsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TwoZoneClock", "settings.xml"); } }

        public ClockForm(bool isPreview, EventWaitHandle signal) {
            preview = isPreview; showSignal = signal;
            prefs = preview ? new Preferences() : LoadSettings();
            prefs.Normalize();
            Text = T("鲸娘两地时钟 · 原本地区 / 目标地区", "Whalegirl · Two-Zone Clock");
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
            BackColor = Surface; ForeColor = Ink; TopMost = prefs.AlwaysOnTop;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            using (var g = CreateGraphics()) scale = g.DpiX / 96f;
            clockIcon = CreateClockIcon(); Icon = clockIcon;
            menu = BuildMenu(); ContextMenuStrip = menu;
            ResizeClock();
            if (prefs.HasPosition) Location = KeepVisible(new Point(prefs.X, prefs.Y)); else SnapToCorner();
            now = DateTimeOffset.UtcNow;
            timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += delegate {
                if (showSignal != null && showSignal.WaitOne(0)) Reveal();
                now = DateTimeOffset.UtcNow;
                string stamp = now.ToString(prefs.ShowSeconds ? "yyyyMMddHHmmss" : "yyyyMMddHHmm", CultureInfo.InvariantCulture);
                if (stamp != lastStamp) { lastStamp = stamp; Invalidate(); }
            };
            if (!preview) {
                tray = new NotifyIcon { Icon = clockIcon, Text = T("两地时钟 · 双击显示，右键设置", "Two-Zone Clock · Right-click for settings"), Visible = true, ContextMenuStrip = menu };
                tray.DoubleClick += delegate { Reveal(); };
                timer.Start();
            }
            MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left && !prefs.Locked) { dragging = true; dragCursor = Cursor.Position; dragPosition = Location; Capture = true; } };
            MouseMove += delegate { if (dragging) { var cursor = Cursor.Position; Location = new Point(dragPosition.X + cursor.X - dragCursor.X, dragPosition.Y + cursor.Y - dragCursor.Y); } };
            MouseUp += delegate { if (dragging) { dragging = false; Capture = false; Location = KeepVisible(Location); SaveSettings(); } };
            MouseCaptureChanged += delegate { if (dragging && !Capture) { dragging = false; Location = KeepVisible(Location); SaveSettings(); } };
            DoubleClick += delegate { if (!prefs.Locked) { SnapToCorner(); SaveSettings(); } };
            FormClosing += delegate { if (!preview) SaveSettings(); timer.Stop(); if (tray != null) tray.Visible = false; };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
            Microsoft.Win32.SystemEvents.TimeChanged += TimeChanged;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; } }
        private string T(string zh, string en) { return prefs.English ? en : zh; }
        private int PanelWidth { get { return prefs.ShowSeconds ? 192 : 168; } }
        private const int HeaderHeight = 44;
        private const int RowHeight = 52;
        private const int DifferenceHeight = 20;
        private void ResizeClock() {
            int oldRight = Right;
            int oldBottom = Bottom;
            ClientSize = new Size((int)Math.Round(PanelWidth * scale), (int)Math.Round((HeaderHeight + RowHeight * (prefs.ShowSource ? 2 : 1) + 8 + (prefs.ShowSource ? DifferenceHeight : 0)) * scale));
            using (var p = Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 15 * scale)) { var old = Region; Region = new Region(p); if (old != null) old.Dispose(); }
            if (IsHandleCreated && oldRight > 0) { Left = oldRight - Width; Top = oldBottom - Height; }
            Invalidate();
        }
        private static GraphicsPath Rounded(RectangleF r, float radius) {
            var p = new GraphicsPath(); float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right-d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90); p.AddArc(r.X, r.Bottom-d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        protected override void OnPaint(PaintEventArgs e) { DrawFace(e.Graphics, now); }
        private void DrawFace(Graphics g, DateTimeOffset instant) {
            g.Clear(Surface); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.ScaleTransform(scale, scale);
            float w = ClientSize.Width / scale, h = ClientSize.Height / scale;
            using (var p = Rounded(new RectangleF(0.5f, 0.5f, w-1.5f, h-1.5f), 14))
            using (var pen = new Pen(Border, 1)) g.DrawPath(pen, p);
            WhaleTheme.DrawMascot(g, new RectangleF(5, 0, 48, 48));
            using (var titleFont = new Font("Microsoft YaHei UI", prefs.English ? 10.5f : 11.5f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var detailFont = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var fg = new SolidBrush(Ink)) using (var muted = new SolidBrush(Muted))
            using (var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter }) {
                g.DrawString(T("鲸娘两地时钟", "Whalegirl Clock"), titleFont, fg, new RectangleF(57, 7, w-64, 18), fmt);
                g.DrawString(T("陪你看昼夜", "Two places, one day"), detailFont, muted, new RectangleF(57, 25, w-64, 14), fmt);
            }
            if (prefs.ShowSource) {
                DrawRow(g, HeaderHeight+4, T("原本", "From") + " · " + RegionChoice.ShortName(prefs.SourceTimeZoneId, prefs.English), ClockTime.Read(instant, prefs.SourceTimeZoneId, prefs.TwentyFourHours, prefs.ShowSeconds, prefs.English), true);
            }
            DrawRow(g, HeaderHeight+4+(prefs.ShowSource ? RowHeight : 0), T("目标", "To") + " · " + RegionChoice.ShortName(prefs.TargetTimeZoneId, prefs.English), ClockTime.Read(instant, prefs.TargetTimeZoneId, prefs.TwentyFourHours, prefs.ShowSeconds, prefs.English), false);
            if (prefs.ShowSource) {
                string difference = ClockTime.DifferenceLabel(instant, prefs.SourceTimeZoneId, prefs.TargetTimeZoneId, prefs.English);
                using (var hintFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var hintBrush = new SolidBrush(Muted))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap }) {
                    // Keep even extreme date-line pairs readable without widening the compact clock.
                    float available = PanelWidth - 12;
                    float measured = g.MeasureString(difference, hintFont).Width;
                    if (measured > available) {
                        using (var fitted = new Font(hintFont.FontFamily, hintFont.Size * available / measured, GraphicsUnit.Pixel))
                            g.DrawString(difference, fitted, hintBrush, new RectangleF(6, HeaderHeight + RowHeight * 2 + 5, available, DifferenceHeight), fmt);
                    } else g.DrawString(difference, hintFont, hintBrush, new RectangleF(6, HeaderHeight + RowHeight * 2 + 5, available, DifferenceHeight), fmt);
                }
            }
        }
        private void DrawRow(Graphics g, int y, string title, ClockReading time, bool source) {
            WhaleTheme.DrawCard(g, new RectangleF(5, y, PanelWidth-11, RowHeight-2), source ? WhaleTheme.Day : WhaleTheme.Night, 9);
            using (var titleFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var timeFont = new Font("Segoe UI", 20f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var dateFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var fg = new SolidBrush(Ink)) using (var muted = new SolidBrush(Muted))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter }) {
                g.DrawString(title, titleFont, muted, new RectangleF(6, y+3, PanelWidth-12, 14), fmt);
                g.DrawString(time.Time, timeFont, fg, new RectangleF(6, y+16, PanelWidth-12, 24), fmt);
                g.DrawString(time.Date, dateFont, muted, new RectangleF(6, y+38, PanelWidth-12, 13), fmt);
            }
        }
        private ContextMenuStrip BuildMenu() {
            var result = new ContextMenuStrip();
            WhaleTheme.StyleMenu(result);
            var regions = new ToolStripMenuItem(T("设置原本地区和目标地区…", "Choose original and target regions…"));
            regions.Click += delegate { ConfigureRegions(); };
            var swap = new ToolStripMenuItem(T("交换两个地区", "Swap regions"));
            swap.Click += delegate { string previous = prefs.SourceTimeZoneId; prefs.SourceTimeZoneId = prefs.TargetTimeZoneId; prefs.TargetTimeZoneId = previous; Invalidate(); SaveSettings(); };
            sourceItem = new ToolStripMenuItem(T("同时显示原本地区", "Show original region too")) { Checked = prefs.ShowSource, CheckOnClick = true };
            sourceItem.Click += delegate { prefs.ShowSource = sourceItem.Checked; ResizeClock(); Location = KeepVisible(Location); SaveSettings(); };
            formatItem = new ToolStripMenuItem(T("24 小时制", "24-hour time")) { Checked = prefs.TwentyFourHours, CheckOnClick = true };
            formatItem.Click += delegate { prefs.TwentyFourHours = formatItem.Checked; Invalidate(); SaveSettings(); };
            secondsItem = new ToolStripMenuItem(T("显示秒数", "Show seconds")) { Checked = prefs.ShowSeconds, CheckOnClick = true };
            secondsItem.Click += delegate { prefs.ShowSeconds = secondsItem.Checked; ResizeClock(); Location = KeepVisible(Location); SaveSettings(); };
            topItem = new ToolStripMenuItem(T("保持置顶", "Always on top")) { Checked = prefs.AlwaysOnTop, CheckOnClick = true };
            topItem.Click += delegate { prefs.AlwaysOnTop = topItem.Checked; TopMost = prefs.AlwaysOnTop; SaveSettings(); };
            lockItem = new ToolStripMenuItem(T("锁定位置", "Lock position")) { Checked = prefs.Locked, CheckOnClick = true };
            lockItem.Click += delegate { prefs.Locked = lockItem.Checked; SaveSettings(); };
            var reset = new ToolStripMenuItem(T("放回屏幕右下角", "Move to bottom right")); reset.Click += delegate { SnapToCorner(); Show(); SaveSettings(); };
            var show = new ToolStripMenuItem(T("显示时钟", "Show clock")); show.Click += delegate { Reveal(); };
            var hide = new ToolStripMenuItem(T("暂时隐藏", "Hide clock")); hide.Click += delegate { Hide(); };
            var exit = new ToolStripMenuItem(T("退出", "Exit")); exit.Click += delegate { Close(); };
            var update = new ToolStripMenuItem(T("检查更新…", "Check for updates…"));
            update.Click += delegate { WhaleCommon.UpdateChecker.Show(this, "clock", "0.3.0", prefs.English); };
            var language = new ToolStripMenuItem("中文 / English");
            var chinese = new ToolStripMenuItem("中文") { Checked = !prefs.English };
            var english = new ToolStripMenuItem("English") { Checked = prefs.English };
            chinese.Click += delegate { SetLanguage(false); };
            english.Click += delegate { SetLanguage(true); };
            language.DropDownItems.AddRange(new ToolStripItem[] { chinese, english });
            result.Items.AddRange(new ToolStripItem[] { regions, swap, language, new ToolStripSeparator(), show, hide, sourceItem, formatItem, secondsItem, topItem, lockItem, new ToolStripSeparator(), reset, update, exit });
            return result;
        }
        internal void ConfigureRegions() {
            using (var dialog = new RegionSettingsForm(prefs.SourceTimeZoneId, prefs.TargetTimeZoneId, prefs.TwentyFourHours, prefs.English)) {
                dialog.TopMost = TopMost;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                prefs.SourceTimeZoneId = dialog.SourceTimeZoneId;
                prefs.TargetTimeZoneId = dialog.TargetTimeZoneId;
                SetLanguage(dialog.English);
                now = DateTimeOffset.UtcNow;
                Invalidate(); SaveSettings();
            }
        }
        private void SetLanguage(bool english) {
            prefs.English = english;
            Text = T("鲸娘两地时钟 · 原本地区 / 目标地区", "Whalegirl · Two-Zone Clock");
            var oldMenu = menu;
            menu = BuildMenu(); ContextMenuStrip = menu;
            if (tray != null) {
                tray.ContextMenuStrip = menu;
                tray.Text = T("两地时钟 · 双击显示，右键设置", "Two-Zone Clock · Right-click for settings");
            }
            if (oldMenu != null) oldMenu.Dispose();
            Invalidate(); SaveSettings();
        }
        private void Reveal() { Location = KeepVisible(Location); Show(); Invalidate(); }
        private void SnapToCorner() {
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            int gap = (int)Math.Round(10*scale);
            Location = new Point(work.Right-Width-gap, work.Bottom-Height-gap);
        }
        private Point KeepVisible(Point proposed) {
            var bounds = new Rectangle(proposed, Size); Rectangle work = Screen.FromRectangle(bounds).WorkingArea;
            return new Point(Math.Max(work.Left, Math.Min(proposed.X, work.Right-Width)), Math.Max(work.Top, Math.Min(proposed.Y, work.Bottom-Height)));
        }
        private void DisplayChanged(object sender, EventArgs e) {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(delegate { Location = KeepVisible(Location); Invalidate(); })); } catch (InvalidOperationException) { }
        }
        private void TimeChanged(object sender, EventArgs e) {
            TimeZoneInfo.ClearCachedData();
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(delegate { now = DateTimeOffset.UtcNow; lastStamp = ""; Invalidate(); })); }
            catch (InvalidOperationException) { }
        }
        private static Preferences LoadSettings() {
            try { using (var f = File.OpenRead(SettingsPath)) return (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(f); } catch { return new Preferences(); }
        }
        private void SaveSettings() {
            if (preview) return;
            prefs.X = Left; prefs.Y = Top; prefs.HasPosition = true;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                string temp = SettingsPath + ".tmp";
                using (var f = File.Create(temp)) new XmlSerializer(typeof(Preferences)).Serialize(f, prefs);
                if (File.Exists(SettingsPath)) File.Replace(temp, SettingsPath, null); else File.Move(temp, SettingsPath);
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private static Icon CreateClockIcon() {
            return WhaleTheme.CreateIcon();
        }
        internal void RenderPreview(string path) {
            scale = 1; ResizeClock();
            using (var bmp = new Bitmap(ClientSize.Width, ClientSize.Height)) using (var g = Graphics.FromImage(bmp)) { DrawFace(g, DateTimeOffset.UtcNow); bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
        }
        internal void RenderPreview(string path, bool english, bool seconds) {
            prefs.English = english; prefs.ShowSeconds = seconds;
            RenderPreview(path);
        }
        protected override void Dispose(bool disposing) {
            if (disposing) {
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
                Microsoft.Win32.SystemEvents.TimeChanged -= TimeChanged;
                if (timer != null) timer.Dispose(); if (tray != null) tray.Dispose(); if (menu != null) menu.Dispose(); if (clockIcon != null) clockIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class Program {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [DllImport("shell32.dll", CharSet=CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string id);
        [STAThread] private static void Main(string[] args) {
            SetProcessDPIAware(); SetCurrentProcessExplicitAppUserModelID("TwoZoneClock.Desktop");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--preview") { using (var f = new ClockForm(true,null)) f.RenderPreview(Path.GetFullPath(args[1])); return; }
            if (args.Length == 2 && (args[0] == "--preview-en" || args[0] == "--preview-seconds")) {
                using (var f = new ClockForm(true,null)) f.RenderPreview(Path.GetFullPath(args[1]), true, args[0] == "--preview-seconds"); return;
            }
            if (args.Length == 2 && args[0] == "--preview-settings") {
                using (var f = new RegionSettingsForm("Singapore Standard Time", "Eastern Standard Time", true)) f.RenderPreview(Path.GetFullPath(args[1])); return;
            }
            if (args.Length == 2 && args[0] == "--preview-settings-en") {
                using (var f = new RegionSettingsForm("Singapore Standard Time", "Eastern Standard Time", true, true)) f.RenderPreview(Path.GetFullPath(args[1])); return;
            }
            string suffix = Environment.UserName;
            bool created;
            using (var mutex = new Mutex(true,"Local\\TwoZoneClock_"+suffix,out created))
            using (var signal = new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\TwoZoneClock_Show_"+suffix)) {
                if (!created) { signal.Set(); return; }
                var clock = new ClockForm(false,signal);
                if (args.Length == 1 && args[0] == "--settings") clock.Shown += delegate { clock.BeginInvoke(new Action(clock.ConfigureRegions)); };
                Application.Run(clock);
            }
        }
    }
}
