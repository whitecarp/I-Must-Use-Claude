param([string]$OutputDirectory = '', [switch]$RunTests)
$ErrorActionPreference = 'Stop'
$clockCompiler = (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe')
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot ('artifacts\whalegirl-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$clockOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $clockOutput -Force | Out-Null
$clockExe = Join-Path $clockOutput 'TwoZoneClock.exe'
$clockResourceArguments = @('/resource:assets\whalegirl.png,TwoZoneClock.Whalegirl.png', '/resource:assets\whalegirl.ico,TwoZoneClock.Whalegirl.ico')
Push-Location -LiteralPath $PSScriptRoot
try {
    & $clockCompiler /nologo /target:winexe "/out:$clockExe" /win32icon:assets\whalegirl.ico @clockResourceArguments /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll WorldClock.cs RegionSettingsForm.cs SearchableRegionCombo.cs WhaleTheme.cs
    if ($LASTEXITCODE -ne 0) { throw 'Clock build failed' }
    if ($RunTests) {
        $clockTests = Join-Path $clockOutput 'ClockTests.exe'
        & $clockCompiler /nologo /target:exe /main:ClockTests "/out:$clockTests" @clockResourceArguments /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll WorldClock.cs RegionSettingsForm.cs SearchableRegionCombo.cs WhaleTheme.cs ClockTests.cs
        if ($LASTEXITCODE -ne 0) { throw 'Clock test build failed' }
        & $clockTests
        if ($LASTEXITCODE -ne 0) { throw 'Clock tests failed' }
    }
    Write-Output "Clock build succeeded: $clockExe"
} finally { Pop-Location }
