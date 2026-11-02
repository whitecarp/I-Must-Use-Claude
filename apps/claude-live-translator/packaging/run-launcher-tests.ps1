$ErrorActionPreference='Stop'
$launcherTestOutput=Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\launcher-tests'
New-Item -ItemType Directory -Path $launcherTestOutput -Force | Out-Null
$launcherMetadata=Join-Path $launcherTestOutput 'PortableBuildInfo.cs'
[IO.File]::WriteAllText($launcherMetadata,'namespace DafeiyuPortable { internal static class PortableBuildInfo { internal const string Version="0.9.1"; internal const string PayloadSha256="fixture"; internal const string ManifestSha256="fixture"; } }')
$launcherCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$launcherTests=Join-Path $launcherTestOutput 'PortableLauncherTests.exe'
& $launcherCompiler /nologo /target:exe /main:PortableLauncherTests "/out:$launcherTests" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll (Join-Path $PSScriptRoot 'PortableLauncher.cs') (Join-Path $PSScriptRoot 'PortableLauncherTests.cs') $launcherMetadata
if($LASTEXITCODE -ne 0){throw 'Launcher fixture build failed'}
& $launcherTests
if($LASTEXITCODE -ne 0){throw 'Launcher fixture tests failed'}
