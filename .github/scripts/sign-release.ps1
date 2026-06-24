# Authenticode-signs SmartGoldbergEmu.exe with a PFX certificate.
#
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File .github\scripts\sign-release.ps1
#   powershell -ExecutionPolicy Bypass -File .github\scripts\sign-release.ps1 -Configuration Debug
#   powershell -ExecutionPolicy Bypass -File .github\scripts\sign-release.ps1 -PfxPath scripts\certs\SmartGoldbergEmu.pfx
#
# Profiles (when -PfxPath omitted):
#   Release -> scripts\certs\SmartGoldbergEmu.pfx, else .github\certs\SmartGoldbergEmu-ci.pfx (passwordless, Actions)
#   Debug   -> scripts\certs\SmartGoldbergEmu.pfx (preferred), bin\Debug\SmartGoldbergEmu.exe, publisher SmartGoldbergEmu
#
# Password: -Password (empty string allowed for passwordless PFX), env SGE_SIGN_PFX_PASSWORD, or {pfx}.password
# Timestamp: skipped for .github/certs/*-ci.pfx (test signing). Production PFX uses -TimestampUrl (DigiCert default).
# Env: SGE_SIGN_SKIP_TIMESTAMP=1 forces skip; SGE_SIGN_TIMESTAMP_URL overrides the TSA URL.

param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [string]$PfxPath = '',
    [string]$Password = '',
    [string[]]$Target = @(),
    [string]$Description = '',
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$SkipTimestamp,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

function Get-SignProfile {
    param([string]$Configuration)

    $localCertDir = Join-Path $repoRoot 'scripts\certs'
    $ciCertDir = Join-Path $repoRoot '.github\certs'

    if ($Configuration -eq 'Debug') {
        return @{
            DefaultPfxPath   = Join-Path $localCertDir 'SmartGoldbergEmu.pfx'
            CiPfxPath        = Join-Path $ciCertDir 'SmartGoldbergEmu-ci.pfx'
            DefaultTarget    = Join-Path $repoRoot 'bin\Debug\SmartGoldbergEmu.exe'
            DefaultDescription = 'SmartGoldbergEmu'
            CreateCertHint   = 'powershell -ExecutionPolicy Bypass -File .github\scripts\create-codesign-cert.ps1 -Dev'
            MissingPfxMessage = 'No Debug signing PFX found. Run export-ci-codesign-cert.ps1 or create-codesign-cert.ps1 -Dev.'
        }
    }

    return @{
        DefaultPfxPath   = Join-Path $localCertDir 'SmartGoldbergEmu.pfx'
        CiPfxPath        = Join-Path $ciCertDir 'SmartGoldbergEmu-ci.pfx'
        DefaultTarget    = Join-Path $repoRoot 'bin\Release\SmartGoldbergEmu.exe'
        DefaultDescription = 'SmartGoldbergEmu'
        CreateCertHint   = 'powershell -ExecutionPolicy Bypass -File .github\scripts\create-codesign-cert.ps1'
        MissingPfxMessage = 'No Release signing PFX found. Run export-ci-codesign-cert.ps1 or create-codesign-cert.ps1.'
    }
}

function Test-CiTestSigningPfx {
    param([string]$PfxResolved)
    return $PfxResolved -match '[\\/]\.github[\\/]certs[\\/]SmartGoldbergEmu-ci\.pfx$'
}

function Resolve-SkipTimestamp {
    param(
        [string]$PfxResolved,
        [switch]$ExplicitSkip
    )

    if ($ExplicitSkip) { return $true }
    $envSkip = $env:SGE_SIGN_SKIP_TIMESTAMP
    if ($envSkip -eq '1' -or $envSkip -eq 'true') { return $true }
    return (Test-CiTestSigningPfx -PfxResolved $PfxResolved)
}

function Find-SignTool {
    $sdkRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $sdkRoot) {
        $latestSdk = Get-ChildItem -LiteralPath $sdkRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^\d+\.\d+' } |
            Sort-Object {
                try { [version]$_.Name } catch { [version]'0.0' }
            } -Descending |
            Select-Object -First 1
        if ($latestSdk) {
            $candidate = Join-Path $latestSdk.FullName 'x64\signtool.exe'
            if (Test-Path -LiteralPath $candidate) { return $candidate }
        }
    }
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -find 'Windows Kits\10\bin\*\x64\signtool.exe' | Select-Object -First 1
        if ($found) { return $found }
    }
    $onPath = Get-Command signtool -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    throw 'signtool.exe not found. Install Visual Studio or Windows SDK (Signing Tools).'
}

function Resolve-SignPfx {
    param(
        [string]$ExplicitPath,
        [hashtable]$Profile
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $resolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ExplicitPath)
        if (Test-Path $resolved) {
            return $resolved
        }
        throw "PFX not found: $resolved"
    }

    if (Test-Path $Profile.DefaultPfxPath) {
        return $Profile.DefaultPfxPath
    }

    if ($Profile.ContainsKey('CiPfxPath') -and (Test-Path $Profile.CiPfxPath)) {
        return $Profile.CiPfxPath
    }

    throw @(
        $Profile.MissingPfxMessage
        "Create one: $($Profile.CreateCertHint)"
    ) -join ' '
}

function Resolve-PfxPassword {
    param(
        [string]$PfxResolved,
        [string]$ExplicitPassword,
        [switch]$NonInteractive
    )

    if ($PSBoundParameters.ContainsKey('Password')) {
        if ([string]::IsNullOrEmpty($ExplicitPassword)) { return $null }
        return $ExplicitPassword
    }

    if ($env:SGE_SIGN_PFX_PASSWORD -ne $null) {
        if ([string]::IsNullOrEmpty($env:SGE_SIGN_PFX_PASSWORD)) { return $null }
        return $env:SGE_SIGN_PFX_PASSWORD
    }

    $passwordFile = "$PfxResolved.password"
    if (Test-Path $passwordFile) {
        $fromFile = (Get-Content -LiteralPath $passwordFile -Raw).Trim()
        if ([string]::IsNullOrEmpty($fromFile)) { return $null }
        return $fromFile
    }

    if ($PfxResolved -match '[\\/]\.github[\\/]certs[\\/]SmartGoldbergEmu-ci\.pfx$') {
        return $null
    }

    if ($NonInteractive) {
        throw @(
            "PFX password required for $PfxResolved."
            'Set env SGE_SIGN_PFX_PASSWORD or create a one-line password file:'
            "  $passwordFile"
        ) -join ' '
    }

    $secureInput = Read-Host 'PFX password' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureInput)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

$profile = Get-SignProfile -Configuration $Configuration
$pfxResolved = Resolve-SignPfx -ExplicitPath $PfxPath -Profile $profile

if ($Target.Count -eq 0) {
    $Target = @($profile.DefaultTarget)
}

if ([string]::IsNullOrWhiteSpace($Description)) {
    $Description = $profile.DefaultDescription
}

$resolvedTargets = @()
foreach ($t in $Target) {
    $path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($t)
    if (-not (Test-Path $path)) {
        throw "Target not found: $path"
    }
    $resolvedTargets += $path
}

$pfxPassword = Resolve-PfxPassword -PfxResolved $pfxResolved -ExplicitPassword $Password -NonInteractive:$NonInteractive
$skipTimestamp = Resolve-SkipTimestamp -PfxResolved $pfxResolved -ExplicitSkip:$SkipTimestamp
$timestampToUse = $TimestampUrl
if ($env:SGE_SIGN_TIMESTAMP_URL) {
    $timestampToUse = $env:SGE_SIGN_TIMESTAMP_URL
}

$signtool = Find-SignTool
Write-Host "Using signtool: $signtool"
Write-Host "Using PFX: $pfxResolved"
Write-Host "Configuration: $Configuration"
if ($skipTimestamp) {
    Write-Host 'RFC 3161 timestamp: skipped (CI/test PFX or SGE_SIGN_SKIP_TIMESTAMP).'
}
else {
    Write-Host "RFC 3161 timestamp: $timestampToUse"
}

foreach ($path in $resolvedTargets) {
    Write-Host "Signing $path ..."
    $args = @('sign', '/f', $pfxResolved)
    if ($null -ne $pfxPassword) {
        $args += '/p', $pfxPassword
    }
    $args += '/fd', 'SHA256', '/d', $Description, '/v'
    if (-not $skipTimestamp) {
        $args += '/tr', $timestampToUse, '/td', 'SHA256'
    }
    $args += $path
    & $signtool @args
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed with exit code $LASTEXITCODE for $path"
    }
}

$pfxPassword = $null

Write-Host ""
Write-Host "Signing complete."
