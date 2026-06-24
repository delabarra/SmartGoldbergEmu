# Build GitHub Release body text from commits since the previous tag.
# Usage (Actions): format-release-notes.ps1 -Tag v2.4.0
# Writes multiline "body" to GITHUB_OUTPUT when set.

param(
    [Parameter(Mandatory)]
    [string]$Tag
)

function Get-PreviousTag {
    param([string]$CurrentTag)

    $tags = @(git tag --sort=-v:refname 2>$null)
    for ($i = 0; $i -lt $tags.Count; $i++) {
        if ($tags[$i] -eq $CurrentTag) {
            if ($i + 1 -lt $tags.Count) {
                return $tags[$i + 1]
            }
            return $null
        }
    }

    return $null
}

function Write-GitHubOutputBody {
    param([string]$Body)

    if ([string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
        Write-Output $Body
        return
    }

    $delimiter = 'RELEASE_NOTES_EOF'
    "body<<$delimiter" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
    $Body | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
    $delimiter | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
}

$previousTag = Get-PreviousTag -CurrentTag $Tag
$logRange = if ($previousTag) { "${previousTag}..${Tag}" } else { $Tag }

$repo = $env:GITHUB_REPOSITORY
if ($repo) {
    $format = "- %s ([%h](https://github.com/$repo/commit/%H))"
}
else {
    $format = '- %s (%h)'
}

$entries = @(git log $logRange --pretty=format:$format --no-merges 2>$null)
if ($entries.Count -eq 0) {
    $heading = '## Changes'
    if ($previousTag) {
        $body = "${heading}`n`n_No commits between ${previousTag} and ${Tag}._"
    }
    else {
        $body = "${heading}`n`n_No commits found for ${Tag}._"
    }
}
else {
    $body = "## Changes`n`n" + ($entries -join "`n")
}

Write-GitHubOutputBody -Body $body
