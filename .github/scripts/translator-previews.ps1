# Synthetic saved-reply and README previews on a disposable GitHub-hosted Windows runner only.
# 150% outputs are explicitly resampled comparisons, not independent OS-DPI tests.
param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$ScreenshotDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:WHALE_E2E_ALLOW_SYSTEM_CHANGES -ne '1' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Refusing translator previews: explicitly enabled GitHub-hosted Windows runners only.'
}
if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { throw 'RUNNER_TEMP is required.' }
$previewPrefix = [IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd('\') + '\'
function Assert-PreviewPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($previewPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Preview inputs and outputs must be below RUNNER_TEMP.' }
    for ($cursor = $full; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse points are not allowed in preview paths.' }
    }
    return $full
}
$Executable = Assert-PreviewPath $Executable
$ScreenshotDirectory = Assert-PreviewPath $ScreenshotDirectory
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw 'Build the translator components first.' }
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($Executable).FileVersion -ne '0.9.5.0') { throw 'Expected translator 0.9.5.0.' }
if (Test-Path -LiteralPath $ScreenshotDirectory) { throw 'Preview output already exists; use a fresh directory to avoid stale screenshots.' }

$previewRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$previewSource = Join-Path $previewRepo 'apps/claude-live-translator/tests/FavoritesCaptureHarness.cs'
if (-not (Test-Path -LiteralPath $previewSource -PathType Leaf)) { throw 'FavoritesCaptureHarness.cs is missing.' }
$previewWork = Join-Path $ScreenshotDirectory 'work'
$previewNativeWork = Join-Path $previewWork 'fixture-96dpi'
$previewNative = Join-Path $ScreenshotDirectory 'native-100'
$previewScaled = Join-Path $ScreenshotDirectory 'scaled-preview-150'
New-Item -ItemType Directory -Path $previewWork,$previewNative,$previewScaled -Force | Out-Null
$previewCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$previewHarness = Join-Path $previewWork 'FavoritesCaptureHarness.exe'
& $previewCompiler /nologo /target:exe /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:$previewHarness" $previewSource
if ($LASTEXITCODE -ne 0) { throw 'Favorites preview harness compilation failed.' }

$previewProcess = $null
$previewOutput = ''
$previewError = ''
try {
    $previewStart = [Diagnostics.ProcessStartInfo]::new()
    $previewStart.FileName = $previewHarness
    $previewStart.WorkingDirectory = $previewWork
    $previewStart.UseShellExecute = $false
    $previewStart.CreateNoWindow = $true
    $previewStart.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $previewStart.RedirectStandardOutput = $true
    $previewStart.RedirectStandardError = $true
    $previewStart.ArgumentList.Add($Executable)
    $previewStart.ArgumentList.Add($previewNativeWork)
    $previewStart.ArgumentList.Add('100')
    $previewStart.Environment['DAFEIYU_DATA_DIR'] = Join-Path $previewWork 'isolated-settings'
    $previewStart.Environment['DAFEIYU_FIXTURE_BACKEND'] = ''
    $previewProcess = [Diagnostics.Process]::Start($previewStart)
    $previewOutputTask = $previewProcess.StandardOutput.ReadToEndAsync()
    $previewErrorTask = $previewProcess.StandardError.ReadToEndAsync()
    if (-not $previewProcess.WaitForExit(180000)) { throw 'Synthetic translator previews exceeded their 180 second deadline.' }
    $previewOutput = $previewOutputTask.GetAwaiter().GetResult()
    $previewError = $previewErrorTask.GetAwaiter().GetResult()
    if ($previewProcess.ExitCode -ne 0) { throw "Synthetic translator preview failed: $previewOutput $previewError" }
} finally {
    if ($previewProcess) {
        try { if (-not $previewProcess.HasExited) { $previewProcess.Kill(); [void]$previewProcess.WaitForExit(5000) } }
        finally { $previewProcess.Dispose() }
    }
    $previewOutput | Set-Content -LiteralPath (Join-Path $ScreenshotDirectory 'capture-stdout.txt') -Encoding utf8
    $previewError | Set-Content -LiteralPath (Join-Path $ScreenshotDirectory 'capture-stderr.txt') -Encoding utf8
}

Add-Type -AssemblyName System.Drawing
$previewRows = [Collections.Generic.List[object]]::new()
$previewCases = @('01-before-save','02-after-save','03-tag-menu','04-settings','05-notebook','06-notebook-search','07-no-matches','08-empty-notebook','09-compose','10-offline-retry','11-retry-dialog','12-glossary')
foreach ($previewLanguage in @('zh','en')) {
    foreach ($previewCase in $previewCases) {
        $previewName = $previewLanguage + '-' + $previewCase
        $previewInput = Join-Path $previewNativeWork ($previewName + '.png')
        if (-not (Test-Path -LiteralPath $previewInput -PathType Leaf) -or (Get-Item -LiteralPath $previewInput).Length -lt 1024) { throw "Missing or unexpectedly small preview: $previewName" }
        $previewImage = [Drawing.Bitmap]::new($previewInput)
        try {
            if ($previewImage.Width -lt 80 -or $previewImage.Height -lt 20 -or $previewImage.Width -gt 2000 -or $previewImage.Height -gt 2000) { throw "Unexpected preview dimensions: $previewName" }
            $previewColors = [Collections.Generic.HashSet[int]]::new()
            for ($x = 0; $x -lt $previewImage.Width; $x += 7) { for ($y = 0; $y -lt $previewImage.Height; $y += 7) { [void]$previewColors.Add($previewImage.GetPixel($x,$y).ToArgb()) } }
            if ($previewColors.Count -lt 12) { throw "Blank or nearly uniform preview: $previewName" }
            $previewNativeFile = Join-Path $previewNative ($previewName + '.png')
            Copy-Item -LiteralPath $previewInput -Destination $previewNativeFile
            $previewRows.Add([pscustomobject]@{ name=$previewName; path=('native-100/' + $previewName + '.png'); scalePercent=100; actualControlDpi=96; method='Actual WinForms controls at a 96-DPI thread context, DrawToBitmap'; width=$previewImage.Width; height=$previewImage.Height; sha256=(Get-FileHash -LiteralPath $previewNativeFile -Algorithm SHA256).Hash.ToLowerInvariant() })

            $previewScaledFile = Join-Path $previewScaled ($previewName + '.png')
            $previewResized = [Drawing.Bitmap]::new([int][Math]::Round($previewImage.Width * 1.5), [int][Math]::Round($previewImage.Height * 1.5))
            try {
                $previewGraphics = [Drawing.Graphics]::FromImage($previewResized)
                try {
                    $previewGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                    $previewGraphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                    $previewGraphics.DrawImage($previewImage, [Drawing.Rectangle]::new(0,0,$previewResized.Width,$previewResized.Height))
                } finally { $previewGraphics.Dispose() }
                $previewResized.Save($previewScaledFile, [Drawing.Imaging.ImageFormat]::Png)
                $previewRows.Add([pscustomobject]@{ name=$previewName; path=('scaled-preview-150/' + $previewName + '.png'); scalePercent=150; actualControlDpi=96; method='Resampled size comparison only; NOT an independent Windows 150-percent DPI layout test'; width=$previewResized.Width; height=$previewResized.Height; sha256=(Get-FileHash -LiteralPath $previewScaledFile -Algorithm SHA256).Hash.ToLowerInvariant() })
            } finally { $previewResized.Dispose() }
        } finally { $previewImage.Dispose() }
    }
}
Copy-Item -LiteralPath (Join-Path $previewNativeWork 'capture-log.txt') -Destination (Join-Path $ScreenshotDirectory 'capture-log.txt')
$previewNotes = @'
Synthetic samples only. No user account, actual reply, real application window, clipboard, translation request or send action is used.
native-100 contains actual Windows Forms control renders under a 96-DPI thread context. Harness assertions verify saving is explicit and bilingual notebook search works.
09-12 are README previews: the app's own fixed-sample RenderPreview output (Write in Chinese, offline retry, retry dialog) and the glossary editor with an excerpt of built-in terms.
scaled-preview-150 contains enlarged PNG comparisons, NOT independently rendered 144-DPI controls. Windows display settings are unchanged.
The existing FavoritesCaptureHarness 150 mode selects system awareness; it needs a real 144-DPI desktop and cannot manufacture that DPI on a standard 96-DPI GitHub runner.
These offscreen DrawToBitmap fixtures do not validate DWM title bars, taskbar integration, focus or actual desktop selection.
Upload only native-100/*.png, scaled-preview-150/*.png and top-level logs/receipts; work/ contains generated executable and synthetic notebook fixtures.
'@
$previewNotes | Set-Content -LiteralPath (Join-Path $ScreenshotDirectory 'CAPTURE-NOTES.txt') -Encoding utf8
[ordered]@{
    schema=1; appVersion='0.9.5'; passed=($previewRows.Count -eq 48); createdUtc=[DateTime]::UtcNow.ToString('o')
    executableSha256=(Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash.ToLowerInvariant()
    syntheticOnly=$true; systemDpiChanged=$false; independent150PercentDpiVerified=$false; previews=$previewRows.ToArray()
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ScreenshotDirectory 'preview-results.json') -Encoding utf8
Write-Output '24 native 96-DPI translator previews and 24 clearly labelled 150% size comparisons passed.'
