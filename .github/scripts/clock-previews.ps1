# Render explicit synthetic preview modes on the disposable CI desktop only.
param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$ScreenshotDirectory
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:WHALE_E2E_ALLOW_SYSTEM_CHANGES -ne '1' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Refusing preview launches: explicitly enabled GitHub-hosted Windows runners only.'
}
if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { throw 'RUNNER_TEMP is required.' }
$taskPrefix = [IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd('\') + '\'
function Assert-RunnerPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($taskPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Preview inputs and outputs must be below RUNNER_TEMP.' }
    for ($current = $full; $current; $current = [IO.Path]::GetDirectoryName($current)) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse points are not permitted in preview paths.' }
    }
    return $full
}
$Executable = Assert-RunnerPath $Executable
$ScreenshotDirectory = Assert-RunnerPath $ScreenshotDirectory
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw 'The clock executable was not built.' }
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($Executable).FileVersion -ne '0.5.0.0') { throw 'Expected clock 0.5.0.0 for reset previews.' }
New-Item -ItemType Directory -Path $ScreenshotDirectory -Force | Out-Null
Add-Type -AssemblyName System.Drawing
$cases = @(
    @{ name='clock-reset-zh'; mode='--preview-reset'; kind='synthetic session +2h15m and weekly +3d4h'; width=168; height=228 },
    @{ name='clock-reset-en'; mode='--preview-reset-en'; kind='synthetic session +2h15m and weekly +3d4h'; width=168; height=228 },
    @{ name='clock-empty-zh'; mode='--preview'; kind='empty reset snapshot'; width=168; height=228 },
    @{ name='clock-empty-en'; mode='--preview-en'; kind='empty reset snapshot'; width=168; height=228 },
    @{ name='clock-reset-settings-zh'; mode='--preview-reset-settings'; kind='empty settings, window enumeration disabled'; width=0; height=0 },
    @{ name='clock-reset-settings-en'; mode='--preview-reset-settings-en'; kind='empty settings, window enumeration disabled'; width=0; height=0 }
)
foreach ($phase in @('night','dawn','day','dusk')) {
    $cases += @{ name=('clock-theme-' + $phase + '-150'); mode='--preview-theme'; phase=$phase; kind='synthetic day-phase fixture at 150 percent'; width=252; height=342 }
}
$results = [Collections.Generic.List[object]]::new()
$failures = [Collections.Generic.List[string]]::new()
foreach ($case in $cases) {
    $output = Join-Path $ScreenshotDirectory ($case.name + '.png')
    $entry = [ordered]@{ name=$case.name; mode=$case.mode; fixture=$case.kind; state='running'; width=$null; height=$null; sha256=$null; error=$null }
    $process = $null
    try {
        if (Test-Path -LiteralPath $output) { throw 'Preview output already exists; refusing to accept an old screenshot.' }
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = $Executable
        $start.WorkingDirectory = Split-Path -Parent $Executable
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        $start.ArgumentList.Add($case.mode)
        if ($case.ContainsKey('phase')) { $start.ArgumentList.Add($case.phase) }
        $start.ArgumentList.Add($output)
        if ($case.ContainsKey('phase')) { $start.ArgumentList.Add('--scale'); $start.ArgumentList.Add('1.5') }
        $process = [Diagnostics.Process]::Start($start)
        if (-not $process.WaitForExit(30000)) { throw 'Clock preview exceeded its 30 second deadline.' }
        if ($process.ExitCode -ne 0) { throw "Clock preview failed with exit code $($process.ExitCode)." }
        if (-not (Test-Path -LiteralPath $output -PathType Leaf) -or (Get-Item -LiteralPath $output).Length -lt 1024) { throw 'Preview PNG is missing or unexpectedly small.' }
        $image = [Drawing.Bitmap]::new($output)
        try {
            if ($case.width -gt 0 -and ($image.Width -ne $case.width -or $image.Height -ne $case.height)) { throw 'Preview dimensions changed unexpectedly.' }
            if ($case.width -eq 0 -and ($image.Width -lt 580 -or $image.Height -lt 430 -or $image.Width -gt 1760 -or $image.Height -gt 1350)) { throw 'Reset settings preview has unexpected dimensions.' }
            $colors = [Collections.Generic.HashSet[int]]::new()
            for ($x=0; $x -lt $image.Width; $x+=3) { for ($y=0; $y -lt $image.Height; $y+=3) { [void]$colors.Add($image.GetPixel($x,$y).ToArgb()) } }
            if ($colors.Count -lt 16) { throw 'Preview image is blank or nearly uniform.' }
            $entry.width = $image.Width; $entry.height = $image.Height
        } finally { $image.Dispose() }
        $entry.sha256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
        $entry.state = 'passed'
    } catch {
        $entry.state = 'failed'; $entry.error = $_.Exception.Message
        $failures.Add($case.name + ': ' + $entry.error)
    } finally {
        if ($process) {
            try {
                if (-not $process.HasExited) { $process.Kill(); [void]$process.WaitForExit(5000) }
            } finally { $process.Dispose() }
        }
        $results.Add([pscustomobject]$entry)
        [ordered]@{
            schema=1; kind='clock-explicit-synthetic-previews'; utc=[DateTime]::UtcNow.ToString('o')
            source='Built clock preview modes; no real account or saved user preferences are read.'
            executableSha256=(Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash.ToLowerInvariant()
            passed=($results.Count -eq $cases.Count -and $failures.Count -eq 0); previews=$results.ToArray()
        } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ScreenshotDirectory 'preview-results.json') -Encoding utf8
    }
}
if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
Write-Output "$($results.Count) synthetic clock previews passed."
