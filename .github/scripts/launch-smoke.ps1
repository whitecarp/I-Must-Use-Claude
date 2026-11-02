# Real GUI launches/captures are limited to the disposable CI desktop.
param([Parameter(Mandatory = $true)][string]$ScreenshotDirectory, [Parameter(Mandatory = $true)][string[]]$Executable)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:WHALE_E2E_ALLOW_SYSTEM_CHANGES -ne '1' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Refusing GUI launches: explicitly enabled GitHub-hosted Windows runners only.'
}
if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { throw 'RUNNER_TEMP is required.' }
$taskPrefix = [IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd('\') + '\'
function Assert-RunnerPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($taskPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Launch inputs and outputs must be below RUNNER_TEMP.' }
    for ($current = $full; $current; $current = [IO.Path]::GetDirectoryName($current)) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse points are not permitted in launch paths.' }
    }
    return $full
}
$ScreenshotDirectory = Assert-RunnerPath $ScreenshotDirectory
if ($Executable.Count -ne 4) { throw 'Exactly four built applications are required.' }
$Executable = @($Executable | ForEach-Object { Assert-RunnerPath $_ })
if (@($Executable | Select-Object -Unique).Count -ne 4) { throw 'Four distinct application paths are required.' }
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class WhaleLaunchWindows {
    public sealed class Window { public IntPtr Handle; public string Title, ClassName; public Bounds Bounds; }
    public sealed class Bounds { public int Left, Top, Width, Height; }
    private delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, IntPtr p);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out Rect rect);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    public static Window[] Find(int processId) {
        var windows = new List<Window>();
        EnumWindows(delegate(IntPtr h, IntPtr p) {
            uint pid; GetWindowThreadProcessId(h, out pid); Rect r;
            if (pid != processId || !IsWindowVisible(h) || !GetWindowRect(h, out r) || r.Right-r.Left < 80 || r.Bottom-r.Top < 40) return true;
            var title = new StringBuilder(1024); var cls = new StringBuilder(256);
            GetWindowText(h, title, title.Capacity); GetClassName(h, cls, cls.Capacity);
            // ShowInTaskbar=false clocks are valid forms. MainWindowHandle and
            // WS_EX_TOOLWINDOW are deliberately not used to reject them.
            if (cls.ToString().StartsWith("WindowsForms10.", StringComparison.Ordinal))
                windows.Add(new Window { Handle=h, Title=title.ToString(), ClassName=cls.ToString(), Bounds=new Bounds { Left=r.Left, Top=r.Top, Width=r.Right-r.Left, Height=r.Bottom-r.Top } });
            return true;
        }, IntPtr.Zero);
        return windows.ToArray();
    }
}
'@
New-Item -ItemType Directory -Path $ScreenshotDirectory -Force | Out-Null
$taskResults = [Collections.Generic.List[object]]::new()
$taskFailures = [Collections.Generic.List[string]]::new()
foreach ($path in $Executable) {
    $name = [IO.Path]::GetFileNameWithoutExtension($path)
    $process = $null; $window = $null
    $entry = [ordered]@{ name=$name; path=$path; state='running'; pid=$null; window=$null; screenshot=$null; screenshotSha256=$null; closed=$false; error=$null }
    try {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'Application executable was not built.' }
        $expectedTitle = switch ($name) {
            'TwoZoneClock' { '鲸娘两地时钟|Whalegirl.*Two.Zone Clock' }
            'ClaudeChinese' { '大肥鱼翻译|Whale Translator' }
            '大肥鱼环境助手' { '大肥鱼环境助手|Whale Environment Helper' }
            default { '大肥鱼打包助手|Big Whale Pack Helper' }
        }
        # A visible window is explicitly required for this CI screenshot task.
        $process = Start-Process -FilePath $path -WorkingDirectory (Split-Path -Parent $path) -WindowStyle Normal -PassThru
        $entry.pid = $process.Id
        $deadline = (Get-Date).AddSeconds(35)
        while ((Get-Date) -lt $deadline) {
            $process.Refresh()
            if ($process.HasExited) { throw "Application exited during startup: $($process.ExitCode)." }
            $window = [WhaleLaunchWindows]::Find($process.Id) | Where-Object Title -match $expectedTitle | Sort-Object { $_.Bounds.Width * $_.Bounds.Height } -Descending | Select-Object -First 1
            if ($null -ne $window) { break }
            Start-Sleep -Milliseconds 250
        }
        if ($null -eq $window) { throw 'No visible application form appeared. A tray icon alone is not a successful launch.' }
        [void][WhaleLaunchWindows]::ShowWindow($window.Handle, 5)
        [void][WhaleLaunchWindows]::SetWindowPos($window.Handle, [IntPtr]::Zero, 24, 24, 0, 0, 0x0001 -bor 0x0040)
        [void][WhaleLaunchWindows]::SetForegroundWindow($window.Handle)
        Start-Sleep -Seconds 2
        $process.Refresh()
        if ($process.HasExited) { throw 'Application exited before its screenshot.' }
        $handle = $window.Handle
        $window = [WhaleLaunchWindows]::Find($process.Id) | Where-Object { $_.Handle -eq $handle } | Select-Object -First 1
        if ($null -eq $window) { throw 'Application window disappeared before capture.' }
        $screen = [Windows.Forms.SystemInformation]::VirtualScreen
        $windowBounds = [Drawing.Rectangle]::new($window.Bounds.Left, $window.Bounds.Top, $window.Bounds.Width, $window.Bounds.Height)
        $bounds = $windowBounds
        $intersection = [Drawing.Rectangle]::Intersect($windowBounds, $screen)
        $fullyOnScreen = $intersection.Size -eq $bounds.Size
        $bitmap = $null; $graphics = $null
        try {
            $bitmap = [Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            if ($fullyOnScreen) {
                $graphics.CopyFromScreen($bounds.Location, [Drawing.Point]::Empty, $bounds.Size)
                $captureMethod = 'CopyFromScreen'
            } else {
                # Hosted desktops can be shorter than the helper's minimum size.
                # Capture the real launched HWND in full; never substitute a demo.
                $dc = $graphics.GetHdc()
                try { if (-not [WhaleLaunchWindows]::PrintWindow($window.Handle, $dc, 2)) { throw 'Full-window capture failed.' } }
                finally { $graphics.ReleaseHdc($dc) }
                $captureMethod = 'PrintWindow-full-HWND'
            }
            $colors = [Collections.Generic.HashSet[int]]::new()
            for ($x=0; $x -lt $bitmap.Width; $x+=7) { for ($y=0; $y -lt $bitmap.Height; $y+=7) { [void]$colors.Add($bitmap.GetPixel($x,$y).ToArgb()) } }
            if ($colors.Count -lt 16) { throw 'Captured image is blank or nearly uniform.' }
            $imagePath = Join-Path $ScreenshotDirectory ($name + '.png')
            $bitmap.Save($imagePath, [Drawing.Imaging.ImageFormat]::Png)
            if ((Get-Item -LiteralPath $imagePath).Length -lt 1024) { throw 'Screenshot file is unexpectedly small.' }
            $entry.window = @{ title=$window.Title; class=$window.ClassName; hwnd=$window.Handle.ToInt64(); width=$bounds.Width; height=$bounds.Height; sampledColors=$colors.Count; captureMethod=$captureMethod }
            $entry.screenshot = $imagePath
            $entry.screenshotSha256 = (Get-FileHash -LiteralPath $imagePath -Algorithm SHA256).Hash
            $entry.state = 'passed'
        } finally {
            if ($graphics) { $graphics.Dispose() }
            if ($bitmap) { $bitmap.Dispose() }
        }
    } catch {
        $entry.state = 'failed'; $entry.error = $_.Exception.Message
        $taskFailures.Add($name + ': ' + $entry.error)
    } finally {
        if ($process) {
            try {
                $process.Refresh()
                if (-not $process.HasExited) {
                    foreach ($owned in [WhaleLaunchWindows]::Find($process.Id)) { [void][WhaleLaunchWindows]::PostMessage($owned.Handle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) }
                    if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id -Force -ErrorAction Stop; [void]$process.WaitForExit(5000) }
                }
                $entry.closed = $process.HasExited
                if (-not $entry.closed) { throw 'Owned process did not stop.' }
            } catch {
                $entry.state = 'failed'; $entry.error = 'Cleanup: ' + $_.Exception.Message
                $taskFailures.Add($name + ': ' + $entry.error)
            } finally { $process.Dispose() }
        }
        $taskResults.Add([pscustomobject]$entry)
        [ordered]@{ schema=1; kind='four-app-visible-launch'; utc=[DateTime]::UtcNow.ToString('o'); passed=($taskResults.Count -eq 4 -and $taskFailures.Count -eq 0); applications=$taskResults.ToArray() } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ScreenshotDirectory 'launch-results.json') -Encoding utf8
    }
}
if ($taskFailures.Count -gt 0) { throw ('Launch smoke failed: ' + ($taskFailures -join '; ')) }
Write-Output 'All four apps exposed visible windows; nonblank screenshots and cleanup receipts verified.'
