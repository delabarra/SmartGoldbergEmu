# Bump Version.props for stable releases (MAJOR.MINOR.PATCH, optional .hotfix revision).
# Preview releases do not modify Version.props — use prepare-preview-release.ps1.
# After a bump, refreshes release.yml workflow_dispatch choice labels to the next versions.
#
# Usage:
#   powershell -File .github\scripts\bump-version.ps1 -Bump patch
#   powershell -File .github\scripts\bump-version.ps1 -Bump "hotfix (2.4.3.3)"
#   powershell -File .github\scripts\bump-version.ps1 -Bump minor -WhatIf
#   powershell -File .github\scripts\bump-version.ps1 -ReadOnly
#   powershell -File .github\scripts\bump-version.ps1 -SyncWorkflowChoices
#
# Writes version, tag, assembly_version, is_prerelease, and package_base_name to GITHUB_OUTPUT when set.

param(
    [string]$Bump = 'patch',
    [switch]$WhatIf,
    [switch]$ReadOnly,
    [switch]$SyncWorkflowChoices
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $PSScriptRoot 'version-props.ps1')

if ($ReadOnly -and $SyncWorkflowChoices) {
    throw 'Use either -ReadOnly or -SyncWorkflowChoices, not both.'
}

$propsPath = Get-VersionPropsPath -RepoRoot $repoRoot
$current = Get-VersionPropsState -PropsPath $propsPath

if ($ReadOnly) {
    Write-VersionReleaseOutputs -State $current
    exit 0
}

if ($SyncWorkflowChoices) {
    if ($WhatIf) {
        $labels = Get-ReleaseWorkflowBumpChoiceLabels -State $current
        Write-Host "Would sync release.yml bump choices from $($current.Full):"
        $labels | ForEach-Object { Write-Host "  $_" }
        exit 0
    }

    $changed = Update-ReleaseWorkflowBumpChoices -RepoRoot $repoRoot -State $current
    if ($changed) {
        Write-Host "Synced release.yml bump choices from $($current.Full)."
    }
    else {
        Write-Host "release.yml bump choices already match $($current.Full)."
    }
    exit 0
}

$bumped = Get-BumpedVersionState -State $current -Bump $Bump
$newPrefix = $bumped.Prefix
$revision = $bumped.Revision
$newFull = $bumped.Full
$newSuffix = ''

if ($WhatIf) {
    Write-Host "Would bump ($($bumped.Kind)): $($current.Full) -> $newFull (tag v$newFull)"
    $previewState = @{
        Major    = $bumped.Major
        Minor    = $bumped.Minor
        Patch    = $bumped.Patch
        Revision = $bumped.Revision
        Full     = $newFull
    }
    $labels = Get-ReleaseWorkflowBumpChoiceLabels -State $previewState
    Write-Host 'Would sync release.yml bump choices to:'
    $labels | ForEach-Object { Write-Host "  $_" }
    exit 0
}

Set-VersionPropsState -PropsPath $propsPath -Text $current.Text -Prefix $newPrefix -Suffix $newSuffix -Revision $revision
$newState = Get-VersionPropsState -PropsPath $propsPath
Update-ReleaseWorkflowBumpChoices -RepoRoot $repoRoot -State $newState | Out-Null
Write-VersionReleaseOutputs -State $newState -PreviousVersion $current.Full
