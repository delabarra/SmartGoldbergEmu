# Publish a verified source SHA onto a target branch (default: dev -> main).
# Fast-forwards when possible. If histories diverged, writes a merge commit whose
# tree is exactly the source SHA (main's files become the source branch files).
# Author/committer are the person who clicked Run workflow — not github-actions
# or an AI account.
#
# Usage (from repo root):
#   powershell -File .github\scripts\merge-branch.ps1 -SourceSha <sha> -SourceBranch dev -TargetBranch main -AuthorName login -AuthorEmail "id+login@users.noreply.github.com"

param(
    [Parameter(Mandatory = $true)]
    [string]$SourceSha,
    [Parameter(Mandatory = $true)]
    [string]$SourceBranch,
    [Parameter(Mandatory = $true)]
    [string]$TargetBranch,
    [Parameter(Mandatory = $true)]
    [string]$AuthorName,
    [Parameter(Mandatory = $true)]
    [string]$AuthorEmail
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

function Test-BotIdentity {
    param([string]$Name)
    if ([string]::IsNullOrWhiteSpace($Name)) { return $true }
    if ($Name -match '\[bot\]$') { return $true }
    $blocked = @(
        'github-actions',
        'github-actions[bot]',
        'Copilot',
        'copilot-swe-agent',
        'copilot-swe-agent[bot]',
        'Cursor',
        'cursor[bot]'
    )
    return $blocked -contains $Name
}

function Push-Ref {
    param([string]$Sha, [string]$Branch)
    git push origin "${Sha}:${Branch}"
    if ($LASTEXITCODE -ne 0) { throw "Failed to push $Branch." }
    Write-Host "Pushed $Sha to origin/$Branch."
}

if (Test-BotIdentity -Name $AuthorName) {
    throw "Refusing to update '$TargetBranch' with bot or AI identity '$AuthorName'. Run this workflow from the Actions tab while signed in as yourself."
}

if ($AuthorEmail -notmatch '@users\.noreply\.github\.com$') {
    throw "Author email must be a GitHub noreply address so the update is attributed to your account."
}

$source = $SourceSha.Trim()
if ($source -notmatch '^[0-9a-fA-F]{40}$') {
    throw "SourceSha must be a full 40-character commit SHA."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

git fetch origin $TargetBranch --quiet
if ($LASTEXITCODE -ne 0) { throw "Failed to fetch origin/$TargetBranch." }

git fetch origin $source --quiet
if ($LASTEXITCODE -ne 0) {
    git fetch origin $SourceBranch --quiet
    if ($LASTEXITCODE -ne 0) { throw "Failed to fetch origin/$SourceBranch." }
}

git cat-file -e "${source}^{commit}"
if ($LASTEXITCODE -ne 0) {
    throw "Pinned SHA $source is not available. Re-run the workflow."
}

$target = (git rev-parse "origin/$TargetBranch").Trim()
if ($target -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Could not resolve origin/$TargetBranch."
}

git merge-base --is-ancestor $source $target
if ($LASTEXITCODE -eq 0) {
    Write-Host "origin/$TargetBranch already contains $source. Nothing to push."
    return
}

git merge-base --is-ancestor $target $source
if ($LASTEXITCODE -eq 0) {
    Write-Host "Fast-forward origin/$TargetBranch to $source."
    Push-Ref -Sha $source -Branch $TargetBranch
    return
}

git config user.name $AuthorName
git config user.email $AuthorEmail
git config commit.gpgsign false
$env:GIT_AUTHOR_NAME = $AuthorName
$env:GIT_AUTHOR_EMAIL = $AuthorEmail
$env:GIT_COMMITTER_NAME = $AuthorName
$env:GIT_COMMITTER_EMAIL = $AuthorEmail

$tree = (git rev-parse "${source}^{tree}").Trim()
$message = "Merge branch '$SourceBranch'"
$mergeSha = (git commit-tree $tree -p $target -p $source -m $message).Trim()
if ($LASTEXITCODE -ne 0 -or $mergeSha -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Failed to create merge commit for origin/$TargetBranch."
}

$authorName = (git log -1 --format='%an' $mergeSha).Trim()
Write-Host "Merge commit $mergeSha (tree matches $source)"
Write-Host "Author:    $authorName <$((git log -1 --format='%ae' $mergeSha).Trim())>"
Write-Host "Committer: $((git log -1 --format='%cn <%ce>' $mergeSha).Trim())"

if (Test-BotIdentity -Name $authorName) {
    throw 'Merge commit author resolved to a bot identity; aborting push.'
}

Push-Ref -Sha $mergeSha -Branch $TargetBranch
