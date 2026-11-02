using System;
using System.IO;
using WhaleClaudeHelper;

// Real end-to-end check of the time zone change and undo, through tzutil on an actual Windows install.
// It changes the machine's time zone, so it refuses to run anywhere except a disposable CI runner.
internal static class E2ETimeZoneTests {
    static int checks;
    static void Check(bool ok, string label) {
        if (!ok) throw new Exception("FAILED: " + label);
        checks++;
        Console.WriteLine("ok " + checks + " - " + label);
    }
    static int Main() {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("WHALE_E2E_ALLOW_SYSTEM_CHANGES") != "1") {
            Console.Error.WriteLine("Refusing to run: this test changes the real Windows time zone and is meant only for disposable CI runners.");
            return 2;
        }
        var platform = new WindowsTimeZonePlatform();
        string original = platform.ReadSetting();
        string backupRoot = Path.Combine(Path.GetTempPath(), "WhaleTimeZoneE2E-" + Guid.NewGuid().ToString("N"));
        var service = new TimeZoneChangeService(platform, backupRoot);
        const string home = "China Standard Time";
        const string elsewhere = "Tokyo Standard Time";
        try {
            platform.WriteSetting(home);
            Check(platform.ReadSetting() == home, "runner starts from " + home);

            SystemChangeResult applied = service.Apply("UTC");
            Check(applied.Ok, "apply UTC succeeds: " + applied.MessageEnglish);
            Check(platform.ReadSetting() == "UTC", "Windows now reports UTC");
            Check(Directory.Exists(applied.BackupDirectory), "a backup receipt was written before the change");

            SystemChangeResult restored = service.Restore(applied.BackupDirectory);
            Check(restored.Ok, "undo succeeds: " + restored.MessageEnglish);
            Check(platform.ReadSetting() == home, "Windows is back on " + home);

            SystemChangeResult again = service.Restore(applied.BackupDirectory);
            Check(again.Ok && platform.ReadSetting() == home, "repeating undo is a safe no-op");

            SystemChangeResult second = service.Apply("UTC");
            Check(second.Ok && platform.ReadSetting() == "UTC", "apply UTC a second time");
            platform.WriteSetting(elsewhere);
            SystemChangeResult conflict = service.Restore(second.BackupDirectory);
            Check(!conflict.Ok, "undo refuses when the time zone was changed again later");
            Check(platform.ReadSetting() == elsewhere, "the later change (" + elsewhere + ") is kept, not overwritten");

            Console.WriteLine("PASS " + checks + " real time zone checks on a disposable Windows runner.");
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error);
            return 1;
        } finally {
            try { platform.WriteSetting(original); Console.WriteLine("Runner time zone restored to " + original + "."); } catch (Exception error) { Console.Error.WriteLine("Could not restore the runner time zone: " + error.Message); }
            try { Directory.Delete(backupRoot, true); } catch { }
        }
    }
}
