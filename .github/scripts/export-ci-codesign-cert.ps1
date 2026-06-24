# Create passwordless self-signed PFX for CI / local test signing (.github/certs/SmartGoldbergEmu-ci.pfx).
# release.yml and preview.yml run this before package-release.ps1 (generated per job, not committed).
#
# Usage: powershell -ExecutionPolicy Bypass -File .github\scripts\export-ci-codesign-cert.ps1
# CI signing skips RFC 3161 timestamp (see sign-release.ps1) — faster Actions; not for SmartScreen trust.

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$certDir = Join-Path $repoRoot '.github\certs'
$pfxPath = Join-Path $certDir 'SmartGoldbergEmu-ci.pfx'

New-Item -ItemType Directory -Path $certDir -Force | Out-Null
if (Test-Path $pfxPath) { Remove-Item -LiteralPath $pfxPath -Force }

$notAfter = (Get-Date).AddYears(5)
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject 'CN=SmartGoldbergEmu CI (test signing only)' `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -KeyExportPolicy Exportable `
    -NotAfter $notAfter `
    -CertStoreLocation 'Cert:\CurrentUser\My'

Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password (New-Object System.Security.SecureString) | Out-Null

Write-Host "Created: $pfxPath"
Write-Host "Thumbprint: $($cert.Thumbprint)"
Write-Host "Size: $((Get-Item $pfxPath).Length) bytes"
