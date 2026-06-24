# Resolve preview release metadata from stable Version.props without mutating the file.
# Preview builds pass /p:VersionSuffix=-preview and /p:VersionBuild=N at MSBuild time.
# Used for both new preview releases and preview re-runs (skip_version_bump).
#
# Usage:
#   powershell -File .github\scripts\prepare-preview-release.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $PSScriptRoot 'version-props.ps1')

$propsPath = Get-VersionPropsPath -RepoRoot $repoRoot
$current = Get-VersionPropsState -PropsPath $propsPath

if (-not [string]::IsNullOrWhiteSpace($current.Suffix)) {
    Write-Warning "Version.props still has suffix '$($current.Suffix)'. Preview releases use MSBuild overrides; main should stay on stable prefix $($current.Prefix)."
}

$previewState = New-PreviewReleaseState -Prefix $current.Prefix
Write-VersionReleaseOutputs -State $previewState
