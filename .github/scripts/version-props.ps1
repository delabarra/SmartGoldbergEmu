# Shared Version.props read/write helpers for release scripts and Actions.

function Get-VersionPropsPath {
    param([string]$RepoRoot)
    return Join-Path $RepoRoot 'Version.props'
}

function New-StableReleaseVersionState {
    param(
        [int]$Major,
        [int]$Minor,
        [int]$Patch,
        [int]$Revision = 0
    )

    $prefix = "$Major.$Minor.$Patch"
    $full = if ($Revision -gt 0) { "$prefix.$Revision" } else { $prefix }

    return @{
        Prefix          = $prefix
        Major           = $Major
        Minor           = $Minor
        Patch           = $Patch
        Revision        = $Revision
        Suffix          = ''
        Full            = $full
        Tag             = "v$full"
        IsPreview       = $false
        PackageBaseName = "SmartGoldbergEmu-$full"
    }
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

    $stable = New-StableReleaseVersionState -Major $major -Minor $minor -Patch $patch -Revision $revision
    $full = if ([string]::IsNullOrWhiteSpace($suffix)) { $stable.Full } else { "$($stable.Full)$suffix" }

    return @{
        Text            = $text
        Prefix          = $stable.Prefix
        Major           = $stable.Major
        Minor           = $stable.Minor
        Patch           = $stable.Patch
        Revision        = $stable.Revision
        Suffix          = $suffix
        Full            = $full
        Tag             = "v$full"
        IsPreview       = -not [string]::IsNullOrWhiteSpace($suffix)
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

function ConvertFrom-StableReleaseVersionText {
    param([string]$VersionText)

    if ([string]::IsNullOrWhiteSpace($VersionText)) {
        throw 'Release version text is empty.'
    }

    $text = $VersionText.Trim()
    if ($text.StartsWith('v', [System.StringComparison]::OrdinalIgnoreCase)) {
        $text = $text.Substring(1)
    }

    # Ignore preview / build metadata — stable release tags are numeric only.
    if ($text -match '[-+]') {
        return $null
    }

    if ($text -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:\.(?<revision>\d+))?$') {
        return $null
    }

    return New-StableReleaseVersionState `
        -Major ([int]$Matches['major']) `
        -Minor ([int]$Matches['minor']) `
        -Patch ([int]$Matches['patch']) `
        -Revision $(if ($Matches['revision']) { [int]$Matches['revision'] } else { 0 })
}

function Compare-StableReleaseVersion {
    param(
        [hashtable]$Left,
        [hashtable]$Right
    )

    foreach ($part in @('Major', 'Minor', 'Patch', 'Revision')) {
        $delta = [int]$Left[$part] - [int]$Right[$part]
        if ($delta -ne 0) {
            return [Math]::Sign($delta)
        }
    }

    return 0
}

# Highest stable vMAJOR.MINOR.PATCH(.HOTFIX) tag in this clone (fetch-tags required in CI).
function Get-LatestStableReleaseTagState {
    param([string]$RepoRoot)

    $tags = @(
        & git -C $RepoRoot tag -l 'v*' 2>$null
    )
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not list git tags (git tag -l v*).'
    }

    $latest = $null
    foreach ($tag in $tags) {
        $parsed = ConvertFrom-StableReleaseVersionText -VersionText $tag
        if ($null -eq $parsed) {
            continue
        }

        if ($null -eq $latest -or (Compare-StableReleaseVersion -Left $parsed -Right $latest) -gt 0) {
            $latest = $parsed
        }
    }

    return $latest
}

# max(Version.props, latest stable tag) across MAJOR.MINOR.PATCH.HOTFIX.
function Resolve-StableReleaseBumpBaseline {
    param(
        [hashtable]$FileState,
        [string]$RepoRoot
    )

    $tagState = Get-LatestStableReleaseTagState -RepoRoot $RepoRoot
    if ($null -eq $tagState) {
        Write-Host 'No stable release tags found; bumping from Version.props.'
        return $FileState
    }

    Write-Host "Latest stable tag: $($tagState.Tag)"
    $cmp = Compare-StableReleaseVersion -Left $tagState -Right $FileState
    if ($cmp -gt 0) {
        Write-Host "Version.props ($($FileState.Full)) is behind $($tagState.Tag); bumping from the tag."
        return $tagState
    }

    if ($cmp -lt 0) {
        Write-Host "Version.props ($($FileState.Full)) is ahead of $($tagState.Tag); bumping from the file."
    }
    else {
        Write-Host "Version.props matches latest tag $($tagState.Tag)."
    }

    return $FileState
}

function Test-GitRefExists {
    param(
        [string]$RepoRoot,
        [string]$Ref
    )

    & git -C $RepoRoot rev-parse -q --verify $Ref 2>$null | Out-Null
    return ($LASTEXITCODE -eq 0)
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
