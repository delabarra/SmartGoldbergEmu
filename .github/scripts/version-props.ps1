# Shared Version.props read/write helpers for release scripts and Actions.

function Get-VersionPropsPath {
    param([string]$RepoRoot)
    return Join-Path $RepoRoot 'Version.props'
}

function Get-VersionPropsState {
    param([string]$PropsPath)

    if (-not (Test-Path $PropsPath)) { throw 'Version.props is missing.' }

    $text = Get-Content -LiteralPath $PropsPath -Raw
    if ($text -notmatch '<VersionPrefix>\s*([^<]+?)\s*</VersionPrefix>') {
        throw 'Could not read VersionPrefix from Version.props.'
    }

    $prefix = $Matches[1].Trim()
    if ($prefix -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$') {
        throw "VersionPrefix '$prefix' is not major.minor.patch."
    }

    $major = [int]$Matches['major']
    $minor = [int]$Matches['minor']
    $patch = [int]$Matches['patch']

    $suffix = ''
    if ($text -match '<VersionSuffix>\s*([^<]*?)\s*</VersionSuffix>') {
        $suffix = $Matches[1].Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($suffix) -and $suffix -ne '-preview') {
        throw "VersionSuffix '$suffix' is not supported (expected empty or -preview)."
    }

    $revision = 0
    if ($text -match '<VersionRevision(?:\s[^>]*)?>\s*([^<]*?)\s*</VersionRevision>') {
        $revisionText = $Matches[1].Trim()
        if ($revisionText -match '^\d+$') {
            $revision = [int]$revisionText
        }
    }

    $numeric = if ($revision -gt 0) { "$prefix.$revision" } else { $prefix }
    $full = if ([string]::IsNullOrWhiteSpace($suffix)) { $numeric } else { "$numeric$suffix" }

    return @{
        Text     = $text
        Prefix   = $prefix
        Major    = $major
        Minor    = $minor
        Patch    = $patch
        Revision = $revision
        Suffix   = $suffix
        Full     = $full
        Tag      = "v$full"
        IsPreview = -not [string]::IsNullOrWhiteSpace($suffix)
        PackageBaseName = "SmartGoldbergEmu-$full"
    }
}

function Set-VersionPropsState {
    param(
        [string]$PropsPath,
        [string]$Text,
        [string]$Prefix,
        [string]$Suffix,
        [int]$Revision = 0
    )

    $newText = $Text -replace '(<VersionPrefix>\s*)([^<]+?)(\s*</VersionPrefix>)', "`${1}$Prefix`${3}"

    if ($newText -match '<VersionSuffix>\s*[^<]*?\s*</VersionSuffix>') {
        $newText = $newText -replace '(<VersionSuffix>\s*)([^<]*?)(\s*</VersionSuffix>)', "`${1}$Suffix`${3}"
    }
    else {
        $insert = "    <VersionSuffix>$Suffix</VersionSuffix>`r`n"
        $newText = $newText -replace '(<VersionPrefix>[^<]+</VersionPrefix>\s*\r?\n)', "`${1}$insert"
    }

    if ($newText -notmatch '<VersionRevision') {
        throw 'Version.props is missing VersionRevision (required for hotfix releases).'
    }

    $newText = $newText -replace '(<VersionRevision(?:\s[^>]*)?>\s*)([^<]*?)(\s*</VersionRevision>)', "`${1}$Revision`${3}"

    Set-Content -LiteralPath $PropsPath -Value $newText -Encoding utf8 -NoNewline
}

function Get-PreviewSuffix {
    return '-preview'
}

function New-PreviewReleaseState {
    param([string]$Prefix)

    if ([string]::IsNullOrWhiteSpace($Prefix)) {
        throw 'Preview release prefix is required.'
    }

    $suffix = Get-PreviewSuffix
    $full = "$Prefix$suffix"

    return @{
        Prefix          = $Prefix
        Suffix          = $suffix
        Full            = $full
        Tag             = "v$full"
        IsPreview       = $true
        PackageBaseName = "SmartGoldbergEmu-$full"
    }
}

function Resolve-BumpKind {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw 'Bump is required.'
    }

    # Accept plain kinds or workflow labels like "patch (2.4.4)".
    if ($Value.Trim() -match '^(?<kind>patch|hotfix|minor|major)(\b|\s|$|\()') {
        return $Matches['kind']
    }

    throw "Unknown bump '$Value' (expected patch, hotfix, minor, or major)."
}

function Get-BumpedVersionState {
    param(
        [hashtable]$State,
        [string]$Bump
    )

    $kind = Resolve-BumpKind -Value $Bump
    $major = [int]$State.Major
    $minor = [int]$State.Minor
    $patch = [int]$State.Patch
    $revision = [int]$State.Revision

    switch ($kind) {
        'patch' { $patch++; $revision = 0 }
        'hotfix' { $revision++ }
        'minor' { $minor++; $patch = 0; $revision = 0 }
        'major' { $major++; $minor = 0; $patch = 0; $revision = 0 }
        default { throw "Unknown bump '$kind'." }
    }

    $prefix = "$major.$minor.$patch"
    $full = if ($revision -gt 0) { "$prefix.$revision" } else { $prefix }

    return @{
        Kind     = $kind
        Prefix   = $prefix
        Major    = $major
        Minor    = $minor
        Patch    = $patch
        Revision = $revision
        Full     = $full
        Tag      = "v$full"
    }
}

function Get-ReleaseWorkflowBumpChoiceLabels {
    param([hashtable]$State)

    $kinds = @('patch', 'hotfix', 'minor', 'major')
    $labels = @()
    foreach ($kind in $kinds) {
        $next = Get-BumpedVersionState -State $State -Bump $kind
        $labels += "$kind ($($next.Full))"
    }

    return $labels
}

function Get-ReleaseWorkflowPath {
    param([string]$RepoRoot)
    return Join-Path $RepoRoot '.github\workflows\release.yml'
}

# Rewrites the workflow_dispatch bump choice labels from Version.props (GitHub cannot compute these live).
function Update-ReleaseWorkflowBumpChoices {
    param(
        [string]$RepoRoot,
        [hashtable]$State
    )

    $workflowPath = Get-ReleaseWorkflowPath -RepoRoot $RepoRoot
    if (-not (Test-Path $workflowPath)) {
        throw "Release workflow missing: $workflowPath"
    }

    $text = Get-Content -LiteralPath $workflowPath -Raw
    $nl = if ($text -match "`r`n") { "`r`n" } else { "`n" }
    $labels = Get-ReleaseWorkflowBumpChoiceLabels -State $State
    $default = $labels[0]
    $optionsBlock = ($labels | ForEach-Object { "          - $_" }) -join $nl
    $replacement = @(
        '        # bump-choices:begin'
        '        options:'
        $optionsBlock
        "        default: $default"
        '        # bump-choices:end'
    ) -join $nl

    $pattern = '(?ms)[ \t]*# bump-choices:begin\r?\n.*?[ \t]*# bump-choices:end'
    if ($text -notmatch $pattern) {
        throw 'release.yml is missing bump-choices markers (expected # bump-choices:begin/end).'
    }

    $newText = [regex]::Replace($text, $pattern, $replacement)
    if ($newText -eq $text) {
        return $false
    }

    Set-Content -LiteralPath $workflowPath -Value $newText -Encoding utf8 -NoNewline
    return $true
}

function Write-VersionReleaseOutputs {
    param(
        [hashtable]$State,
        [string]$PreviousVersion = ''
    )

    Write-Host "Release version: $($State.Full) (tag $($State.Tag))"
    if ($PreviousVersion) {
        Write-Host "Previous: $PreviousVersion"
    }

    if ($env:GITHUB_OUTPUT) {
        "version=$($State.Full)" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
        "tag=$($State.Tag)" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
        "assembly_version=$($State.Prefix)" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
        "is_prerelease=$($State.IsPreview.ToString().ToLowerInvariant())" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
        "package_base_name=$($State.PackageBaseName)" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
        if ($PreviousVersion) {
            "previous_version=$PreviousVersion" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
        }
    }
}

function Get-ReleasePackageBaseNameFromExe {
    param([string]$ExePath)

    if (-not (Test-Path $ExePath)) { throw "Release exe not found: $ExePath" }

    $productVersion = ''
    try {
        $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath)
        if (-not [string]::IsNullOrWhiteSpace($info.ProductVersion)) {
            $productVersion = $info.ProductVersion.Trim()
        }
    }
    catch {
    }

    if (-not [string]::IsNullOrWhiteSpace($productVersion)) {
        $isPreview = $productVersion -match '-preview'
        $label = $productVersion
        if ($label.StartsWith('v', [System.StringComparison]::OrdinalIgnoreCase)) {
            $label = $label.Substring(1)
        }

        $plus = $label.IndexOf('+')
        if ($plus -ge 0) {
            $label = $label.Substring(0, $plus).Trim()
        }

        if ($isPreview -and $label -notmatch 'preview') {
            $label = "$label-preview"
        }

        if (-not [string]::IsNullOrWhiteSpace($label)) {
            return "SmartGoldbergEmu-$label"
        }
    }

    $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($ExePath).Version.ToString(3)
    return "SmartGoldbergEmu-$assemblyVersion"
}

function Test-ProductVersionMatchesRelease {
    param(
        [string]$ProductVersion,
        [string]$ReleaseVersion
    )

    if ([string]::IsNullOrWhiteSpace($ProductVersion)) {
        return $true
    }

    $expectedBase = "v$ReleaseVersion"
    if ($ProductVersion -eq $expectedBase) {
        return $true
    }

    $buildPrefix = "${expectedBase}+build."
    if ($ProductVersion.StartsWith($buildPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $suffix = $ProductVersion.Substring($buildPrefix.Length)
        return $suffix -match '^\d+$'
    }

    return $false
}
