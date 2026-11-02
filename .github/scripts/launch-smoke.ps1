# Starts each built app on the CI runner, waits for its main window, saves a screenshot and closes it.
# A crash at start-up fails the step; screenshots are kept as an artifact for a quick visual check.
param([Parameter(Mandatory = $true)][string]$ScreenshotDirectory, [Parameter(Mandatory = $true)][string[]]$Executable)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
New-Item -ItemType Directory -Path $ScreenshotDirectory -Force | Out-Null
$failures = @()
foreach ($path in $Executable) {
    $name = [IO.Path]::GetFileNameWithoutExtension($path)
    if (-not (Test-Path -LiteralPath $path)) { $failures += "$name was not built"; continue }
    Write-Output "Starting $name"
    $process = Start-Process -FilePath $path -WorkingDirectory (Split-Path -Parent $path) -PassThru
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline -and -not $process.HasExited) {
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($process.HasExited) { $failures += "$name exited during start-up with code $($process.ExitCode)"; continue }
    $title = $process.MainWindowTitle
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { Write-Output "  no main window after 30 s (tray-only apps can look like this)" } else { Write-Output "  window: $title" }
    Start-Sleep -Seconds 2
    try {
        $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
        $bitmap.Save((Join-Path $ScreenshotDirectory ($name + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose(); $bitmap.Dispose()
    } catch { Write-Output "  screenshot unavailable: $($_.Exception.Message)" }
    if (-not $process.HasExited) {
        [void]$process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id -Force }
    }
}
if ($failures.Count -gt 0) { throw ('Launch smoke test failed: ' + ($failures -join '; ')) }
Write-Output "All $($Executable.Count) apps started."
