# Verifies SmartGoldbergEmu.exe has an Authenticode signature (CI test cert or local PFX).
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .github\scripts\verify-release-signature.ps1
#   powershell -ExecutionPolicy Bypass -File .github\scripts\verify-release-signature.ps1 -ExePath bin\Release\SmartGoldbergEmu.exe

param(
    [string]$ExePath = 'bin\Release\SmartGoldbergEmu.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if (-not [System.IO.Path]::IsPathRooted($ExePath)) {
    $ExePath = Join-Path $repoRoot $ExePath
}

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Release exe not found: $ExePath"
}

$sig = Get-AuthenticodeSignature -FilePath $ExePath
if (-not $sig.SignerCertificate) {
    throw "Release exe has no Authenticode signature: $($sig.Status)"
}

Write-Host "Signed by: $($sig.SignerCertificate.Subject)"
