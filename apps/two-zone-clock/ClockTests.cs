using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Serialization;
using TwoZoneClock;

internal static class ClockTests {
    private static int checks;

    private static DateTimeOffset Instant(string value) {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    private static void Assert(bool condition, string description) {
        if (!condition) throw new Exception(description);
        checks++;
    }

    private static void Check(string utc, string timeZoneId, string expected, string expectedZone) {
        var instant = Instant(utc);
        var reading = ClockTime.Read(instant, timeZoneId, true, false);
        string actual = reading.Local.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture);
        Assert(actual == expected, timeZoneId + " at " + utc + ": expected " + expected + ", got " + actual);
        Assert(reading.Local.UtcDateTime == instant.UtcDateTime, timeZoneId + " changed the represented instant");
        Assert(!String.IsNullOrWhiteSpace(reading.Zone), timeZoneId + " has no visible zone label");
        if (expectedZone != null) Assert(reading.Zone == expectedZone, timeZoneId + ": expected " + expectedZone + ", got " + reading.Zone);
    }

    private static Preferences ReadPreferences(string xml) {
        using (var reader = new StringReader(xml)) {
            var restored = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(reader);
            restored.Normalize();
            return restored;
        }
    }

    private static Preferences RoundTrip(Preferences original) {
        using (var writer = new StringWriter(CultureInfo.InvariantCulture)) {
            new XmlSerializer(typeof(Preferences)).Serialize(writer, original);
            return ReadPreferences(writer.ToString());
        }
    }

    private static void CheckPreferences() {
        var defaults = new Preferences();
        defaults.Normalize();
        Assert(defaults.SourceTimeZoneId == "Singapore Standard Time", "Default source must remain Singapore");
        Assert(defaults.TargetTimeZoneId == "Eastern Standard Time", "Default target must remain US Eastern");
        Assert(defaults.ShowSource, "Both regions should be visible by default");
        Assert(!defaults.English, "Default interface language must remain Chinese");

        foreach (string invalid in new string[] { null, "", "   ", "Not/A/Real/TimeZone" }) {
            var prefs = new Preferences { SourceTimeZoneId = invalid, TargetTimeZoneId = invalid };
            prefs.Normalize();
            Assert(prefs.SourceTimeZoneId == "Singapore Standard Time", "Invalid source did not fall back to Singapore");
            Assert(prefs.TargetTimeZoneId == "Eastern Standard Time", "Invalid target did not fall back to US Eastern");
        }

        var independent = new Preferences { SourceTimeZoneId = "Not/A/Real/TimeZone", TargetTimeZoneId = "India Standard Time" };
        independent.Normalize();
        Assert(independent.SourceTimeZoneId == "Singapore Standard Time" && independent.TargetTimeZoneId == "India Standard Time", "Invalid source reset the valid target");
        independent.SourceTimeZoneId = "Central Standard Time";
        independent.TargetTimeZoneId = "Not/A/Real/TimeZone";
        independent.Normalize();
        Assert(independent.SourceTimeZoneId == "Central Standard Time" && independent.TargetTimeZoneId == "Eastern Standard Time", "Invalid target reset the valid source");

        var original = new Preferences {
            SourceTimeZoneId = "Central Standard Time", TargetTimeZoneId = "India Standard Time",
            ShowSource = false, TwentyFourHours = true, ShowSeconds = true,
            AlwaysOnTop = false, Locked = true, HasPosition = true, X = -1250, Y = 372,
            English = true
        };
        var restored = RoundTrip(original);
        Assert(restored.SourceTimeZoneId == original.SourceTimeZoneId, "Source region did not survive XML roundtrip");
        Assert(restored.TargetTimeZoneId == original.TargetTimeZoneId, "Target region did not survive XML roundtrip");
        Assert(restored.ShowSource == original.ShowSource, "Source visibility did not survive XML roundtrip");
        Assert(restored.English, "English interface preference did not survive XML roundtrip");
        Assert(restored.TwentyFourHours == original.TwentyFourHours && restored.ShowSeconds == original.ShowSeconds, "Time format preferences did not survive XML roundtrip");
        Assert(restored.AlwaysOnTop == original.AlwaysOnTop && restored.Locked == original.Locked, "Window preferences did not survive XML roundtrip");
        Assert(restored.HasPosition && restored.X == original.X && restored.Y == original.Y, "Saved position did not survive XML roundtrip");

        var local = RoundTrip(new Preferences { SourceTimeZoneId = "local", TargetTimeZoneId = "local" });
        Assert(local.SourceTimeZoneId == "local" && local.TargetTimeZoneId == "local", "Follow-system choice was replaced by a fixed region");

        var legacy = ReadPreferences("<Preferences><ShowSingapore>false</ShowSingapore><TwentyFourHours>true</TwentyFourHours><ShowSeconds>true</ShowSeconds><AlwaysOnTop>false</AlwaysOnTop><Locked>true</Locked><HasPosition>true</HasPosition><X>-1250</X><Y>372</Y></Preferences>");
        Assert(!legacy.ShowSource, "Legacy hidden Singapore setting did not migrate to hidden source");
        Assert(!legacy.English, "Legacy settings must keep the Chinese interface");
        Assert(legacy.SourceTimeZoneId == "Singapore Standard Time" && legacy.TargetTimeZoneId == "Eastern Standard Time", "Legacy settings did not gain the original default regions");
        Assert(legacy.TwentyFourHours && legacy.ShowSeconds && !legacy.AlwaysOnTop && legacy.Locked && legacy.HasPosition && legacy.X == -1250 && legacy.Y == 372, "Legacy window/time preferences were lost");
        Assert(!RoundTrip(legacy).ShowSource, "Migrated hidden-source preference was lost after saving again");
    }

    private static void CheckLocalAndFormatting() {
        Assert(ClockTime.ResolveZone("local").Id == TimeZoneInfo.Local.Id, "Local choice does not resolve to the system zone");
        Assert(ClockTime.ResolveZone("India Standard Time").Id == "India Standard Time", "Explicit region resolves to a different zone");
        foreach (string utc in new string[] { "2026-01-01T12:34:56Z", "2026-07-01T12:34:56Z" }) {
            var instant = Instant(utc);
            var expected = TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.Local);
            var actual = ClockTime.Read(instant, "local", true, true).Local;
            Assert(actual.DateTime == expected.DateTime && actual.Offset == expected.Offset, "System-local reading differs at " + utc);
        }

        var noon = ClockTime.Read(Instant("2026-01-01T17:00:00Z"), "Eastern Standard Time", false, false);
        var midnight = ClockTime.Read(Instant("2026-01-01T05:00:00Z"), "Eastern Standard Time", false, true);
        var seconds = ClockTime.Read(Instant("2026-01-01T18:30:09Z"), "India Standard Time", true, true);
        Assert(noon.Time == "12:00 PM", "12-hour noon format failed: " + noon.Time);
        Assert(midnight.Time == "12:00:00 AM", "12-hour midnight format failed: " + midnight.Time);
        Assert(seconds.Time == "00:00:09", "24-hour seconds format failed: " + seconds.Time);
        Assert(seconds.Date == "01/02/2026  周五", "Displayed date/weekday did not follow the selected region: " + seconds.Date);
        var english = ClockTime.Read(Instant("2026-01-01T18:30:09Z"), "India Standard Time", true, true, true);
        Assert(english.Date == "01/02/2026  Fri", "English date/weekday did not follow the selected region: " + english.Date);
        Assert(english.Local.DateTime == seconds.Local.DateTime && english.Local.Offset == seconds.Local.Offset && english.Time == seconds.Time, "Changing language changed the represented local time");
    }

    private static void Difference(string utc, string source, string target, string expectedEnglish, string expectedChinese) {
        var instant = Instant(utc);
        Assert(ClockTime.DifferenceLabel(instant, source, target, true) == expectedEnglish, "Wrong English difference for " + source + " to " + target + " at " + utc);
        Assert(ClockTime.DifferenceLabel(instant, source, target, false) == expectedChinese, "Wrong Chinese difference for " + source + " to " + target + " at " + utc);
    }

    private static void CheckTimeDifferences() {
        Difference("2026-01-01T00:00:00Z", "Singapore Standard Time", "Central Standard Time", "To: 14h behind · yesterday", "目标慢 14小时 · 昨天");
        Difference("2026-07-01T12:00:00Z", "Singapore Standard Time", "Central Standard Time", "To: 13h behind · same day", "目标慢 13小时 · 同一天");
        Difference("2026-01-01T23:59:00Z", "Central Standard Time", "Singapore Standard Time", "To: 14h ahead · tomorrow", "目标快 14小时 · 明天");
        Difference("2026-01-01T00:00:00Z", "Singapore Standard Time", "China Standard Time", "To: same time · same day", "目标与原本同一时间 · 同一天");
        Difference("2026-01-01T00:00:00Z", "Singapore Standard Time", "India Standard Time", "To: 2h 30m behind · same day", "目标慢 2小时30分 · 同一天");
        Difference("2026-01-01T00:00:00Z", "India Standard Time", "Nepal Standard Time", "To: 15m ahead · same day", "目标快 15分 · 同一天");

        // The date label follows the target's calendar date relative to the source, not the PC date.
        Difference("2026-01-01T15:59:00Z", "Singapore Standard Time", "Eastern Standard Time", "To: 13h behind · same day", "目标慢 13小时 · 同一天");
        Difference("2026-01-01T16:00:00Z", "Singapore Standard Time", "Eastern Standard Time", "To: 13h behind · yesterday", "目标慢 13小时 · 昨天");
        Difference("2026-12-31T16:00:00Z", "Singapore Standard Time", "Eastern Standard Time", "To: 13h behind · yesterday", "目标慢 13小时 · 昨天");
        Difference("2026-01-01T10:00:00Z", "Dateline Standard Time", "Line Islands Standard Time", "To: 26h ahead · 2 days later", "目标快 26小时 · 晚 2 天");
        Difference("2026-01-01T10:00:00Z", "Line Islands Standard Time", "Dateline Standard Time", "To: 26h behind · 2 days earlier", "目标慢 26小时 · 早 2 天");

        // Recalculate offsets on either side of both US DST transitions; do not cache base offsets.
        Difference("2026-03-08T07:59:00Z", "Singapore Standard Time", "Central Standard Time", "To: 14h behind · same day", "目标慢 14小时 · 同一天");
        Difference("2026-03-08T08:00:00Z", "Singapore Standard Time", "Central Standard Time", "To: 13h behind · same day", "目标慢 13小时 · 同一天");
        Difference("2026-11-01T06:59:00Z", "Singapore Standard Time", "Central Standard Time", "To: 13h behind · same day", "目标慢 13小时 · 同一天");
        Difference("2026-11-01T07:00:00Z", "Singapore Standard Time", "Central Standard Time", "To: 14h behind · same day", "目标慢 14小时 · 同一天");
    }

    private static T NamedControl<T>(Control owner, string name) where T : Control {
        var controls = owner.Controls.Find(name, true);
        if (controls.Length != 1 || !(controls[0] is T)) throw new Exception("Missing or ambiguous control: " + name);
        return (T)controls[0];
    }

    private static ComboBox Selector(RegionSettingsForm dialog, int index) {
        return NamedControl<ComboBox>(dialog, index == 0 ? "sourceSelector" : "targetSelector");
    }

    private static void SelectRegion(ComboBox selector, string id) {
        for (int i = 0; i < selector.Items.Count; i++) {
            if (((RegionChoice)selector.Items[i]).Id == id) {
                selector.SelectedIndex = i;
                return;
            }
        }
        throw new Exception("Region selector is missing " + id);
    }

    private static bool ContainsRegion(ComboBox selector, string id) {
        foreach (RegionChoice choice in selector.Items)
            if (choice.Id == id) return true;
        return false;
    }

    private static RegionChoice Choice(string id, bool english) {
        foreach (RegionChoice choice in RegionChoice.All(english))
            if (choice.Id == id) return choice;
        throw new Exception("Missing region in catalog: " + id);
    }

    private static void CheckBilingualCatalog() {
        foreach (bool english in new bool[] { false, true }) {
            var singapore = Choice("Singapore Standard Time", english);
            Assert(singapore.Matches("新加坡"), "Chinese region search failed in " + (english ? "English" : "Chinese") + " interface");
            Assert(singapore.Matches("sInGaPoRe"), "English case-insensitive region search failed");
            Assert(singapore.Matches(""), "An empty search should retain available regions");
            Assert(!singapore.Matches("no-such-region-987654321"), "An unmatched query returned a region");
            Assert(Choice("Eastern Standard Time", english).Matches("纽约"), "Chinese city search failed");
            Assert(Choice("Eastern Standard Time", english).Matches("nEw YoRk"), "English city search failed");
        }
        Assert(RegionChoice.ShortName("Singapore Standard Time", false).Contains("新加坡"), "Chinese clock region label is not localized");
        Assert(RegionChoice.ShortName("Singapore Standard Time", true).IndexOf("Singapore", StringComparison.OrdinalIgnoreCase) >= 0, "English clock region label is not localized");
    }

    private static void CheckSearchAndLanguageControls() {
        using (var dialog = new RegionSettingsForm("Central Standard Time", "India Standard Time", true, false)) {
            var source = Selector(dialog, 0);
            var target = Selector(dialog, 1);
            var language = NamedControl<ComboBox>(dialog, "languageSelector");
            var save = NamedControl<Button>(dialog, "saveButton");
            int allRegions = RegionChoice.All(false).Length;
            bool hasSeparateSearchBox = false;
            foreach (Control control in dialog.Controls)
                if (control is TextBox) hasSeparateSearchBox = true;
            Assert(!hasSeparateSearchBox, "Settings still contains a separate search text box");
            Assert(dialog.Controls.Find("sourceSearch", true).Length == 0 && dialog.Controls.Find("targetSearch", true).Length == 0, "Search was not integrated into the region selectors");
            Assert(!dialog.English && language.SelectedIndex == 0, "Chinese settings dialog did not initialize its language selector");
            Assert(save.Enabled, "Initial valid selections cannot be saved");

            source.Text = Choice("Singapore Standard Time", false).ToString();
            Assert(dialog.SourceTimeZoneId == "Central Standard Time" && !save.Enabled, "Typing an exact display label silently committed a different region");
            source.Text = "新加坡";
            Assert(ContainsRegion(source, "Singapore Standard Time") && !ContainsRegion(source, "Central Standard Time"), "Chinese search did not filter the source selector");
            Assert(dialog.SourceTimeZoneId == "Central Standard Time" && !save.Enabled, "Filtering silently changed the source region or allowed an unconfirmed choice");
            SelectRegion(source, "Singapore Standard Time");
            Assert(dialog.SourceTimeZoneId == "Singapore Standard Time" && save.Enabled, "Explicit filtered source selection cannot be saved");
            typeof(ComboBox).GetMethod("OnDropDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, new object[] { EventArgs.Empty });
            Assert(source.Items.Count == allRegions && dialog.SourceTimeZoneId == "Singapore Standard Time" && save.Enabled, "Opening the committed source selector did not restore the full catalog and preserve its choice");
            source.Text = "sInGaPoRe";
            Assert(ContainsRegion(source, "Singapore Standard Time") && dialog.SourceTimeZoneId == "Singapore Standard Time" && !save.Enabled, "Mixed-case English search lost the source choice or committed typed text without a selection");
            SelectRegion(source, "Singapore Standard Time");

            target.Text = "nEw YoRk";
            Assert(ContainsRegion(target, "Eastern Standard Time") && !ContainsRegion(target, "India Standard Time"), "English city search did not filter the target selector");
            Assert(dialog.TargetTimeZoneId == "India Standard Time" && !save.Enabled, "Filtering silently changed the target region or allowed an unconfirmed choice");
            SelectRegion(target, "Eastern Standard Time");
            Assert(dialog.TargetTimeZoneId == "Eastern Standard Time" && save.Enabled, "Explicit filtered target selection cannot be saved");
            target.Text = "纽约";
            Assert(ContainsRegion(target, "Eastern Standard Time") && dialog.TargetTimeZoneId == "Eastern Standard Time" && !save.Enabled, "Chinese city search lost the selected target region or committed a pending query");

            source.Text = "no-such-region-987654321";
            Assert(source.Items.Count == 0 && source.SelectedIndex == -1, "Unmatched source search should show no selectable regions");
            Assert(dialog.SourceTimeZoneId == "Singapore Standard Time" && dialog.TargetTimeZoneId == "Eastern Standard Time", "An unmatched search destroyed the chosen regions");
            Assert(!save.Enabled, "Save remained enabled with no matching source region");

            language.SelectedIndex = 1;
            Assert(dialog.English, "English language choice was not returned");
            Assert(dialog.SourceTimeZoneId == "Singapore Standard Time" && dialog.TargetTimeZoneId == "Eastern Standard Time", "Language switch changed the selected regions");
            Assert(source.Text == "no-such-region-987654321" && target.Text == "纽约", "Language switch discarded pending search text");
            Assert(source.Items.Count == 0 && !save.Enabled, "Language switch made an unmatched search saveable");
            Assert(save.Text == "Save", "Save action did not switch to English");

            source.Text = "";
            Assert(source.Items.Count == allRegions && source.SelectedIndex == -1 && source.Text == "" && !save.Enabled, "Clearing the source query did not show all regions while keeping the choice pending");
            Assert(dialog.SourceTimeZoneId == "Singapore Standard Time", "Clearing source search destroyed its committed region");
            SelectRegion(source, "Singapore Standard Time");
            Assert(!save.Enabled, "Confirming the source bypassed the pending target query");
            SelectRegion(target, "Eastern Standard Time");
            Assert(save.Enabled, "Confirming both pending regions did not enable Save");
            target.Text = "no-such-region-987654321";
            Assert(target.Items.Count == 0 && dialog.TargetTimeZoneId == "Eastern Standard Time" && !save.Enabled, "Unmatched target search did not preserve the selection and block Save");
            target.Text = "";
            Assert(target.Items.Count == allRegions && target.SelectedIndex == -1 && target.Text == "" && dialog.TargetTimeZoneId == "Eastern Standard Time" && !save.Enabled, "Clearing target search did not retain the committed ID while awaiting a selection");
            SelectRegion(target, "Eastern Standard Time");

            language.SelectedIndex = 0;
            Assert(!dialog.English && dialog.SourceTimeZoneId == "Singapore Standard Time" && dialog.TargetTimeZoneId == "Eastern Standard Time" && save.Enabled, "Switching back to Chinese lost valid selections");
        }
        using (var english = new RegionSettingsForm("local", "local", true, true)) {
            Assert(english.English && NamedControl<ComboBox>(english, "languageSelector").SelectedIndex == 1, "Saved English preference did not initialize the settings dialog");
            Assert(english.SourceTimeZoneId == "local" && english.TargetTimeZoneId == "local", "English mode lost system-local region selections");
        }
    }

    private static void CheckRegionSettings() {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (RegionChoice choice in RegionChoice.All(false)) {
            Assert(ids.Add(choice.Id), "Duplicate region in catalog: " + choice.Id);
            Assert(ClockTime.IsValidZone(choice.Id), "Invalid region in catalog: " + choice.Id);
        }
        Assert(ids.Contains("local"), "Region catalog is missing the follow-system choice");

        var prefs = new Preferences { SourceTimeZoneId = "Central Standard Time", TargetTimeZoneId = "India Standard Time" };
        using (var dialog = new RegionSettingsForm(prefs.SourceTimeZoneId, prefs.TargetTimeZoneId, false)) {
            Assert(dialog.SourceTimeZoneId == prefs.SourceTimeZoneId, "Source selector did not load the saved region");
            Assert(dialog.TargetTimeZoneId == prefs.TargetTimeZoneId, "Target selector did not load the saved region");
            var source = Selector(dialog, 0);
            var target = Selector(dialog, 1);
            Assert(source.DropDownStyle == ComboBoxStyle.DropDown && target.DropDownStyle == ComboBoxStyle.DropDown, "Region selectors do not allow direct search typing");
            SelectRegion(source, "Singapore Standard Time");
            SelectRegion(target, "Eastern Standard Time");
            Assert(dialog.SourceTimeZoneId == "Singapore Standard Time", "Changed source selection was not returned");
            Assert(dialog.TargetTimeZoneId == "Eastern Standard Time", "Changed target selection was not returned");
            Assert(prefs.SourceTimeZoneId == "Central Standard Time" && prefs.TargetTimeZoneId == "India Standard Time", "Unconfirmed region selections changed existing preferences");
            dialog.DialogResult = DialogResult.Cancel;
        }
        Assert(prefs.SourceTimeZoneId == "Central Standard Time" && prefs.TargetTimeZoneId == "India Standard Time", "Cancelled region selections changed existing preferences");

        using (var local = new RegionSettingsForm("local", "local", true)) {
            Assert(local.SourceTimeZoneId == "local" && local.TargetTimeZoneId == "local", "Both region selectors cannot follow the computer time zone");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ComboBoxInfo {
        public int Size;
        public NativeRect Item, Button;
        public uint ButtonState;
        public IntPtr Combo, Edit, List;
    }
    [DllImport("user32.dll")]
    private static extern bool GetComboBoxInfo(IntPtr window, ref ComboBoxInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static void TypeIntoNativeEdit(ComboBox selector, string query) {
        var info = new ComboBoxInfo { Size = Marshal.SizeOf(typeof(ComboBoxInfo)) };
        if (!GetComboBoxInfo(selector.Handle, ref info) || info.Edit == IntPtr.Zero)
            throw new Exception("Region selector has no native edit field");
        // Target only this fixture's own hidden edit child; no global keyboard input or focus change.
        SendMessage(info.Edit, 0x00B1, IntPtr.Zero, new IntPtr(-1)); // EM_SETSEL: replace current text.
        if (query.Length == 0) SendMessage(info.Edit, 0x0102, new IntPtr(8), IntPtr.Zero);
        foreach (char character in query)
            SendMessage(info.Edit, 0x0102, new IntPtr(character), IntPtr.Zero); // WM_CHAR: native typing route.
    }

    private static void CheckNativeComboTyping() {
        using (var dialog = new RegionSettingsForm("Central Standard Time", "India Standard Time", true, false)) {
            var source = Selector(dialog, 0);
            var target = Selector(dialog, 1);
            var save = NamedControl<Button>(dialog, "saveButton");
            TypeIntoNativeEdit(source, "sInGaPoRe");
            Assert(source.Text == "sInGaPoRe", "Native edit typing lost or replaced the query text");
            Assert(ContainsRegion(source, "Singapore Standard Time") && !ContainsRegion(source, "Central Standard Time"), "Native English typing did not filter the combo results");
            Assert(dialog.SourceTimeZoneId == "Central Standard Time" && !save.Enabled, "Native typing committed a source region before an explicit choice");
            SelectRegion(source, "Singapore Standard Time");
            Assert(save.Enabled && dialog.SourceTimeZoneId == "Singapore Standard Time", "Choosing a native search result did not commit the source region");

            TypeIntoNativeEdit(target, "纽约");
            Assert(target.Text == "纽约" && ContainsRegion(target, "Eastern Standard Time"), "Native Chinese character input did not preserve/filter the query");
            Assert(dialog.TargetTimeZoneId == "India Standard Time" && !save.Enabled, "Native Chinese input silently changed the target region");
            SelectRegion(target, "Eastern Standard Time");
            Assert(save.Enabled && dialog.TargetTimeZoneId == "Eastern Standard Time", "Choosing a native Chinese search result did not commit the target region");

            TypeIntoNativeEdit(source, "no-such-region-987654321");
            Assert(source.Items.Count == 0 && dialog.SourceTimeZoneId == "Singapore Standard Time" && !save.Enabled, "Unmatched native typing did not preserve the source and block Save");
            TypeIntoNativeEdit(source, "");
            Assert(source.Text == "" && source.Items.Count == RegionChoice.All(false).Length && !save.Enabled, "Native deletion did not restore the full catalog as a pending query");
            Assert(!dialog.Visible, "Native combo regression fixture unexpectedly showed a window");
        }
    }

    private static ToolStripMenuItem MenuItem(ToolStripItemCollection items, string text) {
        foreach (ToolStripItem item in items) {
            var menuItem = item as ToolStripMenuItem;
            if (menuItem != null && menuItem.Text == text) return menuItem;
        }
        throw new Exception("Missing menu action: " + text);
    }

    private static void CheckCompactClockAndLanguageMenu() {
        string previewPath = Path.Combine(Path.GetTempPath(), "TwoZoneClock-test-" + Guid.NewGuid().ToString("N") + ".png");
        try {
            using (var clock = new ClockForm(true, null)) {
                clock.RenderPreview(previewPath);
                using (var image = new Bitmap(previewPath)) {
                    Assert(image.Width == 168 && image.Height == 176, "Default preview is not the compact vertically stacked 168 x 176 whalegirl clock with difference footer");
                }
                var prefs = (Preferences)typeof(ClockForm).GetField("prefs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clock);
                string sourceId = prefs.SourceTimeZoneId, targetId = prefs.TargetTimeZoneId;
                var language = MenuItem(clock.ContextMenuStrip.Items, "中文 / English");
                MenuItem(language.DropDownItems, "English").PerformClick();
                Assert(prefs.English, "Clock language menu did not select English");
                Assert(MenuItem(clock.ContextMenuStrip.Items, "Show seconds") != null && MenuItem(clock.ContextMenuStrip.Items, "Choose original and target regions…") != null, "Clock menu did not switch to English");
                Assert(MenuItem(clock.ContextMenuStrip.Items, "Check for updates…") != null, "English check-update action is missing");
                Assert(clock.Text.IndexOf("Two-Zone Clock", StringComparison.Ordinal) >= 0, "Clock window title did not switch to English");
                Assert(prefs.SourceTimeZoneId == sourceId && prefs.TargetTimeZoneId == targetId, "Clock language menu changed the chosen time zones");
                clock.RenderPreview(previewPath);
                using (var image = new Bitmap(previewPath)) {
                    Assert(image.Width == 168 && image.Height == 176, "English preview changed the compact clock dimensions");
                }
                language = MenuItem(clock.ContextMenuStrip.Items, "中文 / English");
                MenuItem(language.DropDownItems, "中文").PerformClick();
                Assert(!prefs.English && MenuItem(clock.ContextMenuStrip.Items, "显示秒数") != null, "Clock language menu did not restore Chinese");
                Assert(MenuItem(clock.ContextMenuStrip.Items, "检查更新…") != null, "Chinese check-update action is missing");
                Assert(prefs.SourceTimeZoneId == sourceId && prefs.TargetTimeZoneId == targetId, "Returning to Chinese changed the chosen time zones");
                MenuItem(clock.ContextMenuStrip.Items, "同时显示原本地区").PerformClick();
                Assert(!prefs.ShowSource, "Single-clock menu did not hide the source");
                clock.RenderPreview(previewPath);
                using (var image = new Bitmap(previewPath)) {
                    Assert(image.Width == 168 && image.Height == 104, "Single-clock mode retained the comparison footer or grew in size");
                }
            }
        } finally {
            if (File.Exists(previewPath)) File.Delete(previewPath);
        }
    }

    [STAThread]
    public static void Main() {
        try { RunAll(); }
        catch (Exception error) { Console.Error.WriteLine("Clock fixture failed after " + checks + " checks: " + error.Message); Environment.ExitCode = 1; }
    }

    private static void RunAll() {
        Check("2026-03-08T06:59:00Z", "Eastern Standard Time", "2026-03-08 01:59 -05:00", "EST");
        Check("2026-03-08T07:00:00Z", "Eastern Standard Time", "2026-03-08 03:00 -04:00", "EDT");
        Check("2026-11-01T05:59:00Z", "Eastern Standard Time", "2026-11-01 01:59 -04:00", "EDT");
        Check("2026-11-01T06:00:00Z", "Eastern Standard Time", "2026-11-01 01:00 -05:00", "EST");
        Check("2026-03-08T07:59:00Z", "Central Standard Time", "2026-03-08 01:59 -06:00", "CST");
        Check("2026-03-08T08:00:00Z", "Central Standard Time", "2026-03-08 03:00 -05:00", "CDT");
        Check("2026-11-01T06:59:00Z", "Central Standard Time", "2026-11-01 01:59 -05:00", "CDT");
        Check("2026-11-01T07:00:00Z", "Central Standard Time", "2026-11-01 01:00 -06:00", "CST");
        Check("2026-01-01T16:00:00Z", "Singapore Standard Time", "2026-01-02 00:00 +08:00", "SGT");
        Check("2026-12-31T16:00:00Z", "Singapore Standard Time", "2027-01-01 00:00 +08:00", "SGT");
        Check("2026-01-01T18:30:00Z", "India Standard Time", "2026-01-02 00:00 +05:30", null);
        CheckLocalAndFormatting();
        CheckTimeDifferences();
        CheckPreferences();
        CheckRegionSettings();
        CheckBilingualCatalog();
        CheckSearchAndLanguageControls();
        CheckNativeComboTyping();
        CheckCompactClockAndLanguageMenu();
        Console.WriteLine(checks + " checks passed: selectable regions, DST, date rollover, time differences, local time, formatting, saved preferences, search, language, and settings controls.");
    }
}
