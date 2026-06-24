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

    $full = if ([string]::IsNullOrWhiteSpace($suffix)) { $prefix } else { "$prefix$suffix" }

    return @{
        Text   = $text
        Prefix = $prefix
        Major  = $major
        Minor  = $minor
        Patch  = $patch
        Suffix = $suffix
        Full   = $full
        Tag    = "v$full"
        IsPreview = -not [string]::IsNullOrWhiteSpace($suffix)
        PackageBaseName = "SmartGoldbergEmu-$full"
    }
}

function Set-VersionPropsState {
    param(
        [string]$PropsPath,
        [string]$Text,
        [string]$Prefix,
        [string]$Suffix
    )

    $newText = $Text -replace '(<VersionPrefix>\s*)([^<]+?)(\s*</VersionPrefix>)', "`${1}$Prefix`${3}"

    if ($newText -match '<VersionSuffix>\s*[^<]*?\s*</VersionSuffix>') {
        $newText = $newText -replace '(<VersionSuffix>\s*)([^<]*?)(\s*</VersionSuffix>)', "`${1}$Suffix`${3}"
    }
    else {
        $insert = "    <VersionSuffix>$Suffix</VersionSuffix>`r`n"
        $newText = $newText -replace '(<VersionPrefix>[^<]+</VersionPrefix>\s*\r?\n)', "`${1}$insert"
    }

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
