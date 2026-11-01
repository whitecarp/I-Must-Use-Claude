using System;
using System.Runtime.InteropServices;
using DafeiyuPortable;

internal static class PortableLauncherTests {
    private static void Check(bool value,string name) { if(!value)throw new Exception(name); }
    private static PayloadFile File(string path) { return new PayloadFile { Path=path,Length=0,Sha256=new string('0',64) }; }
    private static void RejectManifest(PayloadFile[] files,string name) {
        try { PortableLauncher.ValidateManifest(new PayloadManifest { Schema=1,Version=PortableBuildInfo.Version,TotalBytes=0,Files=files }); }
        catch(System.IO.InvalidDataException) { return; }
        throw new Exception(name);
    }
    private static void QuoteRoundTrip(string value) {
        int count;IntPtr argv=CommandLineToArgvW("test.exe "+PortableLauncher.QuoteArgument(value),out count);
        try { Check(argv!=IntPtr.Zero&&count==2,"argument count survives quoting");Check(Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv,IntPtr.Size))==value,"argument preserves exact contents"); }
        finally { if(argv!=IntPtr.Zero)LocalFree(argv); }
    }
    public static int Main() {
        try {
            foreach(string path in new[]{"ClaudeChinese.exe","runtime/python/python312.zip","runtime/venv/Lib/site-packages/numpy/_core/_multiarray_umath.cp312-win_amd64.pyd","licenses/第三方许可.txt"})Check(PortableLauncher.IsSafeRelativePath(path),"safe path accepted");
            foreach(string path in new[]{"","/escape","../escape","runtime/../escape","./file","runtime//file","runtime\\python\\file","C:/escape","runtime/file:stream","runtime/NUL.txt","runtime/COM1","runtime/LPT0.txt","runtime/file.","runtime/file ","runtime/a?b","runtime/a\nb"})Check(!PortableLauncher.IsSafeRelativePath(path),"unsafe path rejected");
            var required=new[]{File("ClaudeChinese.exe"),File("runtime/python/python.exe"),File("backend/offline_worker.py"),File("backend/package_manager.py"),File("backend/package_store.py"),File("backend/argos_catalog.json")};
            PortableLauncher.ValidateManifest(new PayloadManifest { Schema=1,Version=PortableBuildInfo.Version,Files=required,TotalBytes=0 });
            RejectManifest(new[]{File("ClaudeChinese.exe")},"incomplete manifest rejected");
            RejectManifest(new[]{required[0],required[1],required[2]},"package manager and catalog required");
            RejectExtra(required,"claudechinese.exe","case-insensitive duplicate rejected");
            RejectExtra(required,"preferences.json","unapproved payload file rejected");
            RejectExtra(required,"runtime/../escape","traversal manifest rejected");
            RejectExtra(required,"runtime/model/model/model.bin","bundled English-Chinese model rejected");
            RejectExtra(required,"runtime/model-zh-en/sentencepiece.model","bundled Chinese-English tokenizer rejected");
            RejectExtra(required,"runtime/venv/Lib/site-packages/extra/model.bin","model weights cannot hide inside allowed package roots");
            RejectExtra(required,"licenses/package.argosmodel","model archive cannot hide inside license root");
            foreach(string value in new[]{"","simple","has spaces","中文参数","a\"quote","C:\\directory with spaces\\","ends\\\\","line1\nline2"})QuoteRoundTrip(value);
            Console.WriteLine("PortableLauncherTests passed: traversal/device-name guards, base-only payload, required package manager/catalog, manifest duplicates, Windows argument round trips.");return 0;
        }catch(Exception error) { Console.WriteLine("PortableLauncherTests failed: "+error.Message);return 1; }
    }
    private static void RejectExtra(PayloadFile[] required,string path,string name) {
        var files=new PayloadFile[required.Length+1];Array.Copy(required,files,required.Length);files[required.Length]=File(path);RejectManifest(files,name);
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr CommandLineToArgvW(string command,out int count);
    [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr value);
}
