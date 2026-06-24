# Creates a self-signed Authenticode (code signing) certificate for local dev builds.
# Exports a .pfx under scripts\certs\ (local only — never commit).
# CI generates .github\certs\SmartGoldbergEmu-ci.pfx per job via export-ci-codesign-cert.ps1.
#
# Windows still shows "Unknown publisher" until each machine trusts the cert
# (Trusted Root + Trusted Publishers). Self-signed certs do not earn SmartScreen reputation.
#
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File .github\scripts\create-codesign-cert.ps1
#   powershell -ExecutionPolicy Bypass -File .github\scripts\create-codesign-cert.ps1 -Dev
#   powershell -ExecutionPolicy Bypass -File .github\scripts\create-codesign-cert.ps1 -Subject "CN=SmartGoldbergEmu" -ValidYears 5
#
# After export, sign build output:
#   powershell -ExecutionPolicy Bypass -File .github\scripts\sign-release.ps1
#   powershell -ExecutionPolicy Bypass -File .github\scripts\sign-release.ps1 -Configuration Debug
#
# For non-interactive build scripts, store the PFX password in:
#   scripts\certs\SmartGoldbergEmu.pfx.password

param(
    [switch]$Dev,
    [string]$Subject = '',
    [ValidateRange(1, 10)]
    [int]$ValidYears = 3,
    [string]$OutputPfxPath = '',
    [string]$PfxPassword = '',
    [switch]$NoPassword
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$defaultCertDir = Join-Path $repoRoot 'scripts\certs'

if ($Dev) {
    if ([string]::IsNullOrWhiteSpace($Subject)) {
        $Subject = 'CN=SmartGoldbergEmu'
    }
    if ([string]::IsNullOrWhiteSpace($OutputPfxPath)) {
        $OutputPfxPath = Join-Path $defaultCertDir 'SmartGoldbergEmu.pfx'
    }
}
else {
    if ([string]::IsNullOrWhiteSpace($Subject)) {
        $Subject = 'CN=SmartGoldbergEmu'
    }
    if ([string]::IsNullOrWhiteSpace($OutputPfxPath)) {
        $OutputPfxPath = Join-Path $defaultCertDir 'SmartGoldbergEmu.pfx'
    }
}

if (-not [System.IO.Path]::IsPathRooted($OutputPfxPath)) {
    $OutputPfxPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPfxPath)
}

if ($NoPassword) {
    $securePassword = New-Object System.Security.SecureString
}
elseif ([string]::IsNullOrWhiteSpace($PfxPassword)) {
    $secureInput = Read-Host 'PFX export password (remember for sign-release.ps1)' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureInput)
    try {
        $PfxPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
    $securePassword = ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText
}
else {
    $securePassword = ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText
}

$outputDir = Split-Path -Parent $OutputPfxPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

if (Test-Path $OutputPfxPath) {
    throw "Refusing to overwrite existing PFX: $OutputPfxPath. Delete it or pass -OutputPfxPath."
}

Write-Host "Creating self-signed code signing certificate: $Subject"
$notAfter = (Get-Date).AddYears($ValidYears)
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -KeyExportPolicy Exportable `
    -NotAfter $notAfter `
    -CertStoreLocation 'Cert:\CurrentUser\My'

try {
    Export-PfxCertificate -Cert $cert -FilePath $OutputPfxPath -Password $securePassword | Out-Null
}
finally {
    $PfxPassword = $null
    $securePassword = $null
}

Write-Host ""
Write-Host "Certificate created and exported:"
Write-Host "  Subject:  $($cert.Subject)"
Write-Host "  Thumbprint: $($cert.Thumbprint)"
Write-Host "  Valid to: $($cert.NotAfter.ToString('yyyy-MM-dd'))"
Write-Host "  PFX:      $OutputPfxPath"
Write-Host ""
Write-Host "Sign build:"
if ($Dev) {
    Write-Host "  powershell -ExecutionPolicy Bypass -File .github\scripts\sign-release.ps1 -Configuration Debug"
}
else {
    Write-Host "  powershell -ExecutionPolicy Bypass -File .github\scripts\sign-release.ps1"
}
Write-Host ""
Write-Host "To reduce publisher warnings on this PC, import the PFX into:"
Write-Host "  Current User -> Trusted Root Certification Authorities"
Write-Host "  Current User -> Trusted Publishers"
Write-Host "(certmgr.msc or Certificate Manager in certlm/certmgr.)"
