using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows.Forms;

[assembly: AssemblyTitle("大肥鱼打包助手")]
[assembly: AssemblyDescription("Offline file preparation and handoff helper")]
[assembly: AssemblyCompany("大肥鱼")]
[assembly: AssemblyProduct("大肥鱼打包助手")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName=".NET Framework 4.8")]

namespace WhalePack {
    internal static class Program {
        [STAThread]
        private static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new MainForm(SettingsStore.Load())); }
            catch (Exception ex) {
                MessageBox.Show("无法启动 / Unable to start.\n" + ex.Message,
                    "大肥鱼打包助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
