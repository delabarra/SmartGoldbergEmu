# Bump Version.props for stable releases (MAJOR.MINOR.PATCH.HOTFIX).
# Baseline is max(Version.props, latest stable git tag) across all four numbers so
# merge-dev reverting Version.props from an older dev branch cannot republish a tag.
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
$fileState = Get-VersionPropsState -PropsPath $propsPath

if ($ReadOnly) {
    Write-VersionReleaseOutputs -State $fileState
    exit 0
}

$baseline = Resolve-StableReleaseBumpBaseline -FileState $fileState -RepoRoot $repoRoot
$major = $baseline.Major
$minor = $baseline.Minor
$patch = $baseline.Patch
$revision = $baseline.Revision

switch ($Bump) {
    'patch' { $patch++; $revision = 0 }
    'hotfix' { $revision++ }
    'minor' { $minor++; $patch = 0; $revision = 0 }
    'major' { $major++; $minor = 0; $patch = 0; $revision = 0 }
}

$newState = New-StableReleaseVersionState -Major $major -Minor $minor -Patch $patch -Revision $revision

if (Test-GitRefExists -RepoRoot $repoRoot -Ref "refs/tags/$($newState.Tag)") {
    throw "Refusing to bump to $($newState.Full): tag $($newState.Tag) already exists. Choose a different bump or delete the tag."
}

if ($WhatIf) {
    Write-Host "Would bump ($Bump): $($baseline.Full) -> $($newState.Full) (tag $($newState.Tag))"
    exit 0
}

Set-VersionPropsState -PropsPath $propsPath -Text $fileState.Text -Prefix $newState.Prefix -Suffix '' -Revision $newState.Revision
$written = Get-VersionPropsState -PropsPath $propsPath
if ($written.Full -ne $newState.Full) {
    throw "Version.props write mismatch: expected $($newState.Full), got $($written.Full)."
}

Write-VersionReleaseOutputs -State $written -PreviousVersion $baseline.Full
