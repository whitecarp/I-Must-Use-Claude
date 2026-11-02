[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AssetDirectory,
    [Parameter(Mandatory=$true)][string]$BodyPath,
    [Parameter(Mandatory=$true)][string]$Tag,
    [string]$VersionsPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'release-versions.json'),
    [string]$OutputDirectory = '',
    [ValidateSet('unsigned-preview','required')][string]$SigningMode = 'unsigned-preview',
    [switch]$WriteChecksumsToBody,
    [switch]$RequireBodyChecksums
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$assetNames = [ordered]@{
    translator = 'WhaleTranslator.exe'
    clock = 'HomeTimeClock.exe'
    helper = 'WhaleEnvironmentHelper.exe'
    pack = 'WhalePackHelper.exe'
}
$utf8 = New-Object Text.UTF8Encoding($false)

function Read-VersionMap([string]$Text, [string]$Label) {
    $map = $Text | ConvertFrom-Json -ErrorAction Stop
    if ($null -eq $map -or $map -is [array] -or $map -is [string]) { throw "$Label must be a JSON object." }
    $properties = @($map.PSObject.Properties)
    $keyTokens = [regex]::Matches($Text, '"([^"\\]+)"\s*:')
    if ($properties.Count -ne 4 -or $keyTokens.Count -ne 4) { throw "$Label must contain exactly four unique app keys." }
    foreach ($key in $assetNames.Keys) {
        $property = @($properties | Where-Object { $_.Name -ceq $key })
        if ($property.Count -ne 1 -or $property[0].Value -isnot [string]) { throw "$Label is missing the string key '$key'." }
        Convert-AppVersion $property[0].Value "$Label.$key" | Out-Null
    }
    return $map
}

function Convert-AppVersion([string]$Text, [string]$Label) {
    if ($Text -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw "$Label must have three or four numeric version components (got '$Text')." }
    $parts = @($Text.Split('.') | ForEach-Object { [int]$_ })
    if ($parts.Count -eq 3) { $parts += 0 }
    foreach ($part in $parts) { if ($part -gt 65535) { throw "$Label has an out-of-range PE version component." } }
    return ($parts -join '.')
}

function Assert-PeFile([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $reader = New-Object IO.BinaryReader($stream)
    try {
        if ($stream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5a4d) { throw "Not a Windows PE file: $Path" }
        $stream.Position = 0x3c
        $offset = $reader.ReadInt32()
        if ($offset -lt 64 -or $offset -gt ($stream.Length - 4)) { throw "Invalid PE header offset: $Path" }
        $stream.Position = $offset
        if ($reader.ReadUInt32() -ne 0x00004550) { throw "Invalid PE signature: $Path" }
    } finally { $reader.Dispose(); $stream.Dispose() }
}

if ($Tag -notmatch '^v\d+\.\d+\.\d+(?:-[a-zA-Z0-9.-]+)?$') { throw 'Invalid release tag.' }
if ($SigningMode -eq 'unsigned-preview' -and $Tag -notmatch '-preview(?:[.-][a-zA-Z0-9.-]+)?$') {
    throw 'Unsigned releases are allowed only with an explicit -preview tag.'
}
$assetRoot = [IO.Path]::GetFullPath($AssetDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $assetRoot -PathType Container)) { throw 'Asset directory does not exist.' }
if (((Get-Item -LiteralPath $assetRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Asset directory cannot be a filesystem link.' }
$files = @(Get-ChildItem -LiteralPath $assetRoot -Force)
if ($files.Count -ne 4) { throw 'Release assets must be exactly four EXE files; no config, ZIP, checksum attachment, subdirectory, or extra file.' }
foreach ($file in $files) {
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Release asset is not a regular file: $($file.Name)" }
    if ($file.Name -cnotin @($assetNames.Values)) { throw "Unexpected release asset: $($file.Name)" }
}
$manifestText = [IO.File]::ReadAllText([IO.Path]::GetFullPath($VersionsPath))
$manifest = Read-VersionMap $manifestText 'release-versions.json'
$bodyFullPath = [IO.Path]::GetFullPath($BodyPath)
$body = [IO.File]::ReadAllText($bodyFullPath)
$markers = [regex]::Matches($body, '<!--\s*whale-app-versions:\s*(\{[^\r\n]*\})\s*-->')
if ($markers.Count -ne 1 -or [regex]::Matches($body, 'whale-app-versions:').Count -ne 1) {
    throw 'Release body must contain exactly one single-line <!-- whale-app-versions: {...} --> marker.'
}
$bodyVersions = Read-VersionMap $markers[0].Groups[1].Value 'release body'
if ($SigningMode -eq 'unsigned-preview' -and ($body -notmatch '(?i)unsigned' -or $body -notmatch 'SmartScreen')) {
    throw 'Unsigned preview body must explicitly say unsigned and retain the SmartScreen notice.'
}
$records = @()
foreach ($key in $assetNames.Keys) {
    $expectedVersion = Convert-AppVersion $manifest.$key "manifest.$key"
    if ((Convert-AppVersion $bodyVersions.$key "body.$key") -cne $expectedVersion) { throw "Release body version differs from manifest for $key." }
    $path = Join-Path $assetRoot $assetNames[$key]
    Assert-PeFile $path
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($path)
    if ((Convert-AppVersion $version.FileVersion "$key FileVersion") -cne $expectedVersion) { throw "FileVersion differs from manifest for $key." }
    if ((Convert-AppVersion $version.ProductVersion "$key ProductVersion") -cne $expectedVersion) { throw "ProductVersion differs from manifest for $key." }
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    $signatureStatus = [string]$signature.Status
    if ($SigningMode -eq 'required') {
        if ($signatureStatus -ne 'Valid' -or $null -eq $signature.SignerCertificate) { throw "Valid Authenticode signature required for $key (got $signatureStatus). No unsigned fallback." }
    } elseif ($signatureStatus -ne 'NotSigned') {
        throw "Unsigned-preview mode requires all four files to be unsigned (got $signatureStatus for $key); use required mode for signed releases."
    }
    $signer = if ($null -ne $signature.SignerCertificate) { $signature.SignerCertificate.Thumbprint } else { $null }
    $records += [pscustomobject][ordered]@{
        app = $key; name = $assetNames[$key]; version = $manifest.$key
        fileVersion = $version.FileVersion; productVersion = $version.ProductVersion
        bytes = (Get-Item -LiteralPath $path).Length
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        signatureStatus = $signatureStatus; signerThumbprint = $signer
    }
}
$checksumLines = @($records | ForEach-Object { $_.sha256 + '  ' + $_.name })
$checksums = ($checksumLines -join "`n") + "`n"
$fence = ([char]96).ToString() * 3
$checksumBlock = "<!-- whale-sha256:start -->`n" + $fence + "text`n" + $checksums + $fence + "`n<!-- whale-sha256:end -->"
$checksumPattern = '(?s)<!-- whale-sha256:start -->.*?<!-- whale-sha256:end -->'
$checksumMatches = [regex]::Matches($body, $checksumPattern)
$startCount = [regex]::Matches($body, '<!-- whale-sha256:start -->').Count
$endCount = [regex]::Matches($body, '<!-- whale-sha256:end -->').Count
if ($checksumMatches.Count -gt 1 -or $startCount -ne $endCount -or $startCount -ne $checksumMatches.Count) { throw 'Malformed or duplicate release checksum block.' }
if ($WriteChecksumsToBody) {
    if ($checksumMatches.Count -eq 1) { $body = $body.Replace($checksumMatches[0].Value, $checksumBlock) }
    else { $body = $body.TrimEnd() + "`n`n## SHA-256`n`n" + $checksumBlock + "`n" }
} elseif ($checksumMatches.Count -eq 1) {
    if ($checksumMatches[0].Value.Replace("`r`n", "`n") -cne $checksumBlock) { throw 'Release body SHA-256 block does not match the actual EXE bytes.' }
} elseif ($RequireBodyChecksums) { throw 'Release body is missing the generated SHA-256 block.' }

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = $assetRoot + '-validation' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
if ($outputRoot.Equals($assetRoot, [StringComparison]::OrdinalIgnoreCase) -or $outputRoot.StartsWith($assetRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation output must be outside the release asset directory.'
}
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$report = [ordered]@{
    tag = $Tag; validatedUtc = [DateTime]::UtcNow.ToString('o'); signingMode = $SigningMode
    manifestSha256 = (Get-FileHash -LiteralPath $VersionsPath -Algorithm SHA256).Hash.ToLowerInvariant()
    assets = $records
}
[IO.File]::WriteAllText((Join-Path $outputRoot 'SHA256SUMS.txt'), $checksums, $utf8)
[IO.File]::WriteAllText((Join-Path $outputRoot 'SHA256SUMS.md'), $checksumBlock + "`n", $utf8)
[IO.File]::WriteAllText((Join-Path $outputRoot 'validation.json'), ($report | ConvertTo-Json -Depth 6) + "`n", $utf8)
if ($WriteChecksumsToBody) { [IO.File]::WriteAllText($bodyFullPath, $body, $utf8) }
[pscustomobject]@{ Valid = $true; Tag = $Tag; SigningMode = $SigningMode; Assets = $records; ReportDirectory = $outputRoot }
