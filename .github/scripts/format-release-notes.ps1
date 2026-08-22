# Build GitHub Release body text from commits since the previous tag.
# Usage (Actions): format-release-notes.ps1 -Tag v2.4.0
# Writes multiline "body" to GITHUB_OUTPUT when set.

param(
    [Parameter(Mandatory)]
    [string]$Tag
)

# Omit noise from player-facing release notes (matched against the commit subject).
$ExcludeSubjectRegexes = @(
    '^chore\(release\):\s*bump version\b',
    '^Updated achievements icons resolution\.?$'
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

function Test-ExcludedSubject {
    param([string]$Subject)

    if ([string]::IsNullOrWhiteSpace($Subject)) {
        return $true
    }

    foreach ($pattern in $ExcludeSubjectRegexes) {
        if ($Subject -match $pattern) {
            return $true
        }
    }

    return $false
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

# Use a machine-readable record format first so PowerShell never splits on spaces in --pretty=.
$records = @(git log $logRange --pretty=format:'%H%x09%h%x09%s' --no-merges 2>$null)
$entries = New-Object System.Collections.Generic.List[string]

foreach ($record in $records) {
    if ([string]::IsNullOrWhiteSpace($record)) {
        continue
    }

    $parts = $record.Split([char]9, 3)
    if ($parts.Count -lt 3) {
        continue
    }

    $fullHash = $parts[0]
    $shortHash = $parts[1]
    $subject = $parts[2]

    if (Test-ExcludedSubject -Subject $subject) {
        continue
    }

    if ($repo) {
        $entries.Add("- $subject ([${shortHash}](https://github.com/$repo/commit/$fullHash))")
    }
    else {
        $entries.Add("- $subject ($shortHash)")
    }
}

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
