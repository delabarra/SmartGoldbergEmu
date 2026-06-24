# Builds Release and produces a versioned zip under dist/.
# Stages SmartGoldbergEmu.exe (+ .config when present) and README.md from repo root.
# Skips local dev artifacts: *.log, *.cfg, games\, goldberg\.
#
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File .github\scripts\package-release.ps1
#   powershell -ExecutionPolicy Bypass -File .github\scripts\package-release.ps1 -SkipBuild
#   powershell -ExecutionPolicy Bypass -File .github\scripts\package-release.ps1 -NoSign
#   powershell -ExecutionPolicy Bypass -File .github\scripts\package-release.ps1 -SignPfxPath .github\certs\SmartGoldbergEmu-ci.pfx
# Signs when a PFX exists: scripts\certs\SmartGoldbergEmu.pfx (local dev), else .github\certs\SmartGoldbergEmu-ci.pfx
# (Actions generates the CI PFX via export-ci-codesign-cert.ps1 before this script; no TSA round-trip for CI cert).

param(
    [switch]$SkipBuild,
    [switch]$NoSign,
    [string]$SignPfxPath = '',
    [string]$SignPassword = '',
    [string]$PackageBaseName = '',
    [switch]$RequirePreviewPackageName
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot
. (Join-Path $PSScriptRoot 'version-props.ps1')

function Find-MSBuild {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($found) { return $found }
    }
    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    $onPath = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    throw 'MSBuild not found. Install Visual Studio or Build Tools with the MSBuild workload.'
}

$releaseDir = Join-Path $repoRoot 'bin\Release'
$exePath = Join-Path $releaseDir 'SmartGoldbergEmu.exe'

if (-not $SkipBuild) {
    $msbuild = Find-MSBuild
    Write-Host "Building SmartGoldbergEmu.csproj (Release; embeds launcher updater)..."
    & $msbuild (Join-Path $repoRoot 'SmartGoldbergEmu.csproj') /t:Rebuild /p:Configuration=Release /p:Platform=x64 /m /v:m /nologo
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }
}

if (-not (Test-Path $exePath)) {
    throw "Missing $exePath. Build Release first or run without -SkipBuild."
}

$pfxToUse = ''
if (-not $NoSign) {
    if (-not [string]::IsNullOrWhiteSpace($SignPfxPath)) {
        $pfxToUse = $SignPfxPath
    }
    else {
        $localPfx = Join-Path $repoRoot 'scripts\certs\SmartGoldbergEmu.pfx'
        $ciPfx = Join-Path $repoRoot '.github\certs\SmartGoldbergEmu-ci.pfx'
        if (Test-Path $localPfx) { $pfxToUse = $localPfx }
        elseif (Test-Path $ciPfx) { $pfxToUse = $ciPfx }
    }
}

if (-not [string]::IsNullOrWhiteSpace($pfxToUse)) {
    $signScript = Join-Path $PSScriptRoot 'sign-release.ps1'
    if (-not (Test-Path $signScript)) {
        throw "Signing requested but .github\scripts\sign-release.ps1 is missing."
    }
    if (-not (Test-Path $pfxToUse)) {
        throw "Signing PFX not found: $pfxToUse"
    }
    $signArgs = @{
        Configuration  = 'Release'
        PfxPath        = $pfxToUse
        Target         = @($exePath)
        NonInteractive = $true
    }
    if ($PSBoundParameters.ContainsKey('SignPassword')) {
        $signArgs['Password'] = $SignPassword
    }
    elseif ($pfxToUse -match '[\\/]\.github[\\/]certs[\\/]SmartGoldbergEmu-ci\.pfx$') {
        $signArgs['Password'] = ''
    }
    Write-Host "Authenticode signing Release exe ($pfxToUse)..."
    & $signScript @signArgs
    if ($LASTEXITCODE -ne 0) { throw 'sign-release.ps1 failed.' }
}

$propsPath = Get-VersionPropsPath -RepoRoot $repoRoot
$versionProps = $null
if (-not [string]::IsNullOrWhiteSpace($PackageBaseName)) {
    $stageName = $PackageBaseName.Trim()
}
elseif (Test-Path $propsPath) {
    $versionProps = Get-VersionPropsState -PropsPath $propsPath
    $stageName = $versionProps.PackageBaseName
}
else {
    $stageName = Get-ReleasePackageBaseNameFromExe -ExePath $exePath
}

if ($RequirePreviewPackageName -and $stageName -notmatch 'preview') {
    throw "Preview release package name must include 'preview': $stageName"
}

if ($versionProps -and $versionProps.IsPreview -and $stageName -notmatch 'preview') {
    throw "Preview release package name must include 'preview': $stageName"
}

$version = if (-not [string]::IsNullOrWhiteSpace($PackageBaseName)) {
    if ($PackageBaseName.StartsWith('SmartGoldbergEmu-', [System.StringComparison]::OrdinalIgnoreCase)) {
        $PackageBaseName.Substring('SmartGoldbergEmu-'.Length)
    }
    else {
        $PackageBaseName
    }
}
elseif ($versionProps) {
    $versionProps.Full
}
else {
    [System.Reflection.AssemblyName]::GetAssemblyName($exePath).Version.ToString(3)
}
$distRoot = Join-Path $repoRoot 'dist'
$stageRoot = Join-Path $distRoot $stageName
$zipPath = Join-Path $distRoot "$stageName.zip"

if (Test-Path $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null

foreach ($artifact in @('SmartGoldbergEmu.exe', 'SmartGoldbergEmu.exe.config')) {
    $src = Join-Path $releaseDir $artifact
    if (Test-Path $src) {
        Copy-Item -LiteralPath $src -Destination (Join-Path $stageRoot $artifact) -Force
    }
}

$readmeSrc = Join-Path $repoRoot 'README.md'
if (-not (Test-Path $readmeSrc)) { throw 'Required file missing: README.md' }
Copy-Item -LiteralPath $readmeSrc -Destination (Join-Path $stageRoot 'README.md') -Force

$required = @(
    (Join-Path $stageRoot 'SmartGoldbergEmu.exe'),
    (Join-Path $stageRoot 'README.md')
)
foreach ($path in $required) {
    if (-not (Test-Path $path)) { throw "Package validation failed: missing $path" }
}

if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -LiteralPath $stageRoot -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host ""
Write-Host "Release package ready:"
Write-Host "  Stage: $stageRoot"
Write-Host "  Zip:   $zipPath"
Write-Host "  Version: $version ($stageName)"
