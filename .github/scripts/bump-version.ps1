# Bump Version.props for stable releases (MAJOR.MINOR.PATCH, optional .hotfix revision).
# Preview releases do not modify Version.props — use prepare-preview-release.ps1.
#
# Usage:
#   powershell -File .github\scripts\bump-version.ps1 -Bump patch
#   powershell -File .github\scripts\bump-version.ps1 -Bump hotfix
#   powershell -File .github\scripts\bump-version.ps1 -Bump minor -WhatIf
#   powershell -File .github\scripts\bump-version.ps1 -ReadOnly
#
# Writes version, tag, assembly_version, is_prerelease, and package_base_name to GITHUB_OUTPUT when set.

param(
    [ValidateSet('patch', 'hotfix', 'minor', 'major')]
    [string]$Bump = 'patch',
    [switch]$WhatIf,
    [switch]$ReadOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $PSScriptRoot 'version-props.ps1')

$propsPath = Get-VersionPropsPath -RepoRoot $repoRoot
$current = Get-VersionPropsState -PropsPath $propsPath

if ($ReadOnly) {
    Write-VersionReleaseOutputs -State $current
    exit 0
}

$major = $current.Major
$minor = $current.Minor
$patch = $current.Patch
$revision = $current.Revision
$newSuffix = ''

switch ($Bump) {
    'patch' { $patch++; $revision = 0 }
    'hotfix' { $revision++ }
    'minor' { $minor++; $patch = 0; $revision = 0 }
    'major' { $major++; $minor = 0; $patch = 0; $revision = 0 }
}

$newPrefix = "$major.$minor.$patch"
$newFull = if ($revision -gt 0) { "$newPrefix.$revision" } else { $newPrefix }

if ($WhatIf) {
    Write-Host "Would bump ($Bump): $($current.Full) -> $newFull (tag v$newFull)"
    exit 0
}

Set-VersionPropsState -PropsPath $propsPath -Text $current.Text -Prefix $newPrefix -Suffix $newSuffix -Revision $revision
$newState = Get-VersionPropsState -PropsPath $propsPath
Write-VersionReleaseOutputs -State $newState -PreviousVersion $current.Full
