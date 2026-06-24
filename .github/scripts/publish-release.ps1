# Local-only publish: preflight, optional tests/rules, package zip, gh release create.
# Canonical publish path: GitHub Actions workflows release.yml / preview.yml (manual dispatch).
#
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File .github\scripts\publish-release.ps1
#   powershell -ExecutionPolicy Bypass -File .github\scripts\publish-release.ps1 -WhatIf
#
# Bump Version.props (bump-version.ps1) before publish. For CI releases, use Actions instead.

param(
    [switch]$SkipTests,
    [switch]$SkipRules,
    [switch]$SkipBuild,
    [switch]$NoSign,
    [switch]$Force,
    [switch]$WhatIf,
    [string]$Repo = '',
    [string]$SignPfxPath = '',
    [string]$SignPassword = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot
. (Join-Path $PSScriptRoot 'version-props.ps1')

function Get-ReleaseVersionFromProps {
    $propsPath = Get-VersionPropsPath -RepoRoot $repoRoot
    $state = Get-VersionPropsState -PropsPath $propsPath
    return $state.Full
}

function Get-BuiltExeVersionInfo {
    param([string]$ExePath)
    if (-not (Test-Path $ExePath)) { throw "Release exe not found: $ExePath" }
    $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($ExePath).Version.ToString(3)
    $productVersion = ''
    try {
        $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath)
        if (-not [string]::IsNullOrWhiteSpace($info.ProductVersion)) {
            $productVersion = $info.ProductVersion.Trim()
        }
    }
    catch {
    }
    return @{
        AssemblyVersion = $assemblyVersion
        ProductVersion  = $productVersion
        ZipBaseName     = Get-ReleasePackageBaseNameFromExe -ExePath $ExePath
    }
}

function Test-BuiltVersionMatchesProps {
    param(
        [string]$ReleaseVersion,
        [string]$ExePath
    )
    $propsPath = Get-VersionPropsPath -RepoRoot $repoRoot
    $state = Get-VersionPropsState -PropsPath $propsPath
    if ($ReleaseVersion -ne $state.Full) {
        throw "Version.props full version '$($state.Full)' does not match expected '$ReleaseVersion'."
    }

    $built = Get-BuiltExeVersionInfo -ExePath $ExePath
    if ($built.AssemblyVersion -ne $state.Prefix) {
        throw "Built AssemblyVersion $($built.AssemblyVersion) does not match VersionPrefix $($state.Prefix). Rebuild Release."
    }
    if (-not (Test-ProductVersionMatchesRelease -ProductVersion $built.ProductVersion -ReleaseVersion $state.Full)) {
        throw "Built ProductVersion '$($built.ProductVersion)' does not match expected 'v$($state.Full)' (+build optional). Rebuild Release."
    }
    return $built
}

function Assert-GitAvailable {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw 'git is not on PATH.'
    }
}

function Get-GitPorcelainStatus {
    Assert-GitAvailable
    return @(git -C $repoRoot status --porcelain 2>$null)
}

function Get-RemoteTagExists {
    param([string]$Tag)
    Assert-GitAvailable
    $ref = git -C $repoRoot ls-remote --tags origin "refs/tags/$Tag" 2>$null
    return -not [string]::IsNullOrWhiteSpace($ref)
}

function Test-RemoteGitHubReleaseExists {
    param(
        [string]$Gh,
        [string]$GitHubRepo,
        [string]$Tag
    )
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Gh release view $Tag --repo $GitHubRepo 2>$null | Out-Null
        return $LASTEXITCODE -eq 0
    }
    finally {
        $ErrorActionPreference = $prevEap
    }
}

function Get-GitHubRepoFromOrigin {
    if (-not [string]::IsNullOrWhiteSpace($Repo)) {
        if ($Repo -match '^[\w.-]+/[\w.-]+$') { return $Repo.Trim() }
        throw "Invalid -Repo '$Repo'. Use owner/name (e.g. delabarra/SmartGoldbergEmu)."
    }
    Assert-GitAvailable
    $url = git -C $repoRoot remote get-url origin 2>$null
    if ([string]::IsNullOrWhiteSpace($url)) { throw 'No origin remote configured.' }
    if ($url -match 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$') {
        $name = $Matches['repo'] -replace '\.git$', ''
        return "$($Matches['owner'])/$name"
    }
    throw "Could not parse GitHub owner/repo from origin URL: $url"
}

function Find-GhCli {
    $candidates = @(
        (Join-Path ${env:ProgramFiles} 'GitHub CLI\gh.exe'),
        'gh'
    )
    foreach ($c in $candidates) {
        $cmd = Get-Command $c -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }
    return $null
}

function Get-CommandOutputLines {
    param($Output)
    if ($null -eq $Output) { return @() }
    if ($Output -is [string]) { return @($Output) }
    return @($Output | ForEach-Object { $_.ToString() })
}

function Stop-Publish {
    param(
        [string]$Title,
        [string]$Reason,
        [string[]]$Details = @(),
        [string[]]$Actions = @()
    )
    Write-Host ''
    Write-Host $Title -ForegroundColor Red
    Write-Host $Reason
    foreach ($d in $Details) {
        if (-not [string]::IsNullOrWhiteSpace($d)) { Write-Host "  $d" }
    }
    if ($Actions.Count -gt 0) {
        Write-Host ''
        Write-Host 'Next steps:' -ForegroundColor Yellow
        foreach ($a in $Actions) { Write-Host "  $a" }
    }
    exit 1
}

function Invoke-WhatIfStep {
    param([string]$Description)
    Write-Host "[WhatIf] $Description" -ForegroundColor DarkCyan
}

function Invoke-ScriptOrStop {
    param(
        [string]$StepTitle,
        [string]$ScriptPath,
        [hashtable]$Arguments = @{},
        [string]$Reason = '',
        [string[]]$Actions = @()
    )
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        if ($Arguments.Count -gt 0) {
            & $ScriptPath @Arguments
        }
        else {
            & $ScriptPath
        }
        if ($LASTEXITCODE -ne 0) {
            $defaultReason = if ($Reason) { $Reason } else { "$([IO.Path]::GetFileName($ScriptPath)) exited with code $LASTEXITCODE." }
            Stop-Publish -Title "Publish failed: $StepTitle" -Reason $defaultReason -Actions $Actions
        }
    }
    catch {
        Stop-Publish -Title "Publish failed: $StepTitle" -Reason $_.Exception.Message -Actions $Actions
    }
    finally {
        $ErrorActionPreference = $prevEap
    }
}

function Invoke-GitOrStop {
    param(
        [string]$StepTitle,
        [string[]]$Arguments,
        [string]$Reason = '',
        [string[]]$Actions = @()
    )
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = @(git -C $repoRoot @Arguments 2>&1)
        if ($LASTEXITCODE -ne 0) {
            $lines = Get-CommandOutputLines -Output $out
            $defaultReason = if ($Reason) { $Reason } else { "git exited with code $LASTEXITCODE." }
            Stop-Publish -Title "Publish failed: $StepTitle" -Reason $defaultReason -Details $lines -Actions $Actions
        }
    }
    catch {
        Stop-Publish -Title "Publish failed: $StepTitle" -Reason $_.Exception.Message -Actions $Actions
    }
    finally {
        $ErrorActionPreference = $prevEap
    }
}

function Invoke-GhOrStop {
    param(
        [string]$Gh,
        [string]$StepTitle,
        [string[]]$Arguments,
        [string]$Reason = '',
        [string[]]$Actions = @()
    )
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = @(& $Gh @Arguments 2>&1)
        if ($LASTEXITCODE -ne 0) {
            $lines = Get-CommandOutputLines -Output $out
            $defaultReason = if ($Reason) { $Reason } else { "gh exited with code $LASTEXITCODE." }
            Stop-Publish -Title "Publish failed: $StepTitle" -Reason $defaultReason -Details $lines -Actions $Actions
        }
    }
    catch {
        Stop-Publish -Title "Publish failed: $StepTitle" -Reason $_.Exception.Message -Actions $Actions
    }
    finally {
        $ErrorActionPreference = $prevEap
    }
}

function Get-GitChangeDescription {
    param([string]$PorcelainLine)
    if ([string]::IsNullOrWhiteSpace($PorcelainLine)) { return $PorcelainLine }
    if ($PorcelainLine.Length -lt 4) { return $PorcelainLine.Trim() }
    $xy = $PorcelainLine.Substring(0, 2)
    $path = $PorcelainLine.Substring(3).Trim()
    $kind = switch ($xy) {
        '??' { 'untracked' }
        ' M' { 'modified (not staged)' }
        'M ' { 'modified (staged)' }
        'MM' { 'modified (staged + unstaged)' }
        ' A' { 'added (not staged)' }
        'A ' { 'added (staged)' }
        ' D' { 'deleted (not staged)' }
        'D ' { 'deleted (staged)' }
        default { "status '$($xy.Trim())'" }
    }
    return "$path  ($kind)"
}

try {
$propsPath = Get-VersionPropsPath -RepoRoot $repoRoot
$versionState = Get-VersionPropsState -PropsPath $propsPath
$releaseVersion = $versionState.Full
$tag = $versionState.Tag
$isPrerelease = $versionState.IsPreview
$exePath = Join-Path $repoRoot 'bin\Release\SmartGoldbergEmu.exe'
$packageScript = Join-Path $PSScriptRoot 'package-release.ps1'
$checkRulesScript = Join-Path $PSScriptRoot 'check-rules.ps1'
$testsProject = Join-Path $repoRoot 'Tests\Tests.csproj'

Write-Host "SmartGoldbergEmu publish (local)" -ForegroundColor Cyan
Write-Host "  Version.props : $releaseVersion"
Write-Host "  Git tag         : $tag"
Write-Host "  Mode            : gh release create (use Actions release.yml for canonical CI publish)"
Write-Host ""

if ($WhatIf) {
    Invoke-WhatIfStep 'Run check-rules.ps1 (unless -SkipRules)'
    Invoke-WhatIfStep 'Run unit tests (if Tests project exists, unless -SkipTests)'
    Invoke-WhatIfStep 'Run package-release.ps1 (build, sign if PFX present, zip)'
    Invoke-WhatIfStep "Validate exe version matches $releaseVersion / v$releaseVersion"
    Invoke-WhatIfStep 'git push origin main'
    Invoke-WhatIfStep "gh release create $tag dist\SmartGoldbergEmu-$releaseVersion.zip (creates Release + tag)"
    exit 0
}

$dirty = Get-GitPorcelainStatus
if ($dirty.Count -gt 0 -and -not $Force) {
    $fileDetails = foreach ($line in $dirty) { Get-GitChangeDescription -PorcelainLine $line }
    $countLabel = if ($dirty.Count -eq 1) { '1 uncommitted file' } else { "$($dirty.Count) uncommitted files" }
    Stop-Publish `
        -Title "Cannot publish: $countLabel in this repo" `
        -Reason 'Publish requires a clean working tree so the release matches what is committed.' `
        -Details $fileDetails `
        -Actions @(
        'Commit:   git add -A   then   git commit -m "your message"'
        'Stash:    git stash push -m "before release"'
        'Bypass:   publish-release.ps1 -Force   (pushes only what is already committed)'
    )
}

$ghRepo = Get-GitHubRepoFromOrigin
$gh = Find-GhCli

if (-not $gh) {
    Stop-Publish `
        -Title 'Cannot publish: GitHub CLI (gh) not found' `
        -Reason 'gh is required to create the GitHub Release and upload the zip.' `
        -Actions @(
        'Install:  https://cli.github.com/'
        'Login:    gh auth login'
        'Or publish via GitHub Actions: release.yml / preview.yml'
    )
}
if (Test-RemoteGitHubReleaseExists -Gh $gh -GitHubRepo $ghRepo -Tag $tag) {
    Stop-Publish `
        -Title "Cannot publish: GitHub Release $tag already exists" `
        -Reason "Release $tag is already published; bump the version to publish again." `
        -Details @("Repository: $ghRepo") `
        -Actions @(
        "Bump Version.props with bump-version.ps1 (current: $releaseVersion)"
        'Commit the version bump, then run publish-release.ps1 again'
        "Or open:  https://github.com/$ghRepo/releases/tag/$tag"
    )
}
if (Get-RemoteTagExists -Tag $tag) {
    Write-Host "Tag $tag exists on origin without a GitHub Release; will create the Release and attach the zip." -ForegroundColor Yellow
}

if (-not $SkipRules -and (Test-Path $checkRulesScript)) {
    Write-Host 'Running rules check...' -ForegroundColor Cyan
    Invoke-ScriptOrStop -StepTitle 'rules check' -ScriptPath $checkRulesScript `
        -Reason 'One or more project rules failed (see [FAIL] lines above).' `
        -Actions @(
        'Fix the failed checks shown above'
        'Re-run:  powershell -File .github\scripts\check-rules.ps1 -Verbose'
        'Or skip: publish-release.ps1 -SkipRules'
    )
}

if (-not $SkipTests -and (Test-Path $testsProject)) {
    Write-Host 'Running unit tests (Tests)...' -ForegroundColor Cyan
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $testOut = @(dotnet test $testsProject -c Release -v minimal 2>&1)
        if ($LASTEXITCODE -ne 0) {
            $lines = Get-CommandOutputLines -Output $testOut
            Stop-Publish -Title 'Publish failed: unit tests' `
                -Reason 'dotnet test reported failures.' `
                -Details $lines `
                -Actions @(
                'Fix failing tests, then publish again'
                'Or skip: publish-release.ps1 -SkipTests'
            )
        }
    }
    finally {
        $ErrorActionPreference = $prevEap
    }
}

$packageArgs = @{
    SkipBuild = $SkipBuild
    NoSign    = $NoSign
}
if (-not [string]::IsNullOrWhiteSpace($SignPfxPath)) { $packageArgs['SignPfxPath'] = $SignPfxPath }
if (-not [string]::IsNullOrWhiteSpace($SignPassword)) { $packageArgs['SignPassword'] = $SignPassword }

Write-Host 'Packaging release...' -ForegroundColor Cyan
Invoke-ScriptOrStop -StepTitle 'packaging' -ScriptPath $packageScript -Arguments $packageArgs `
    -Reason 'Build or zip step failed (see output above).' `
    -Actions @(
    'Re-run:  powershell -File .github\scripts\package-release.ps1'
    'Ensure Visual Studio / MSBuild is installed'
)

try {
    $built = Test-BuiltVersionMatchesProps -ReleaseVersion $releaseVersion -ExePath $exePath
}
catch {
    Stop-Publish -Title 'Publish failed: version mismatch' -Reason $_.Exception.Message -Actions @(
        'Rebuild Release:  msbuild SmartGoldbergEmu.sln /p:Configuration=Release'
        "Confirm Version.props matches $releaseVersion"
    )
}

$zipPath = Join-Path $repoRoot "dist\$($built.ZipBaseName).zip"
if (-not (Test-Path $zipPath)) {
    Stop-Publish -Title 'Publish failed: missing release zip' `
        -Reason "Expected file was not created: $zipPath" `
        -Actions @('Re-run:  powershell -File .github\scripts\package-release.ps1')
}

Write-Host ''
Write-Host 'Package validated:' -ForegroundColor Green
Write-Host "  Assembly : $($built.AssemblyVersion)"
Write-Host "  Product  : $($built.ProductVersion)"
Write-Host "  Zip      : $zipPath"
Write-Host ''
Write-Host "GitHub repo: $ghRepo" -ForegroundColor Cyan

Write-Host 'Pushing main...' -ForegroundColor Cyan
Invoke-GitOrStop -StepTitle 'git push origin main' -Arguments @('push', 'origin', 'main') `
    -Reason 'Remote rejected the push, or auth/network failed.' `
    -Actions @(
    'Sync first:  git pull --rebase origin main'
    'Then run publish-release.ps1 again'
)

Write-Host "Creating GitHub Release $tag with zip asset..." -ForegroundColor Cyan
$releaseArgs = @(
    'release', 'create', $tag, $zipPath,
    '--repo', $ghRepo,
    '--title', $tag,
    '--notes', "Release $tag"
)
if ($isPrerelease) {
    $releaseArgs += '--prerelease'
}
if (Get-RemoteTagExists -Tag $tag) {
    $releaseArgs += '--verify-tag'
}
Invoke-GhOrStop -Gh $gh -StepTitle "gh release create $tag" -Arguments $releaseArgs `
    -Reason 'GitHub rejected creating the release (auth, permissions, or tag/asset conflict).' `
    -Actions @(
    'Login:  gh auth login'
    "Check:  gh release view $tag --repo $ghRepo"
    "If release exists, bump Version.props (current $releaseVersion)"
)
$prevEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    git -C $repoRoot fetch origin tag $tag 2>$null | Out-Null
}
finally {
    $ErrorActionPreference = $prevEap
}
Write-Host ''
Write-Host "GitHub Release published: https://github.com/$ghRepo/releases/tag/$tag" -ForegroundColor Green

if (-not (Test-Path (Join-Path $repoRoot 'Constants\LauncherReleaseConstants.cs'))) {
    Write-Host 'Reminder: set GitHubOwner and GitHubRepo in Constants/LauncherReleaseConstants.cs for File -> Launcher Update.' -ForegroundColor Yellow
}
else {
    $constantsText = Get-Content (Join-Path $repoRoot 'Constants\LauncherReleaseConstants.cs') -Raw
    if ($constantsText -match 'GitHubOwner\s*=\s*""') {
        Write-Host "Reminder: set LauncherReleaseConstants to your release repo (e.g. $ghRepo) for in-app updates." -ForegroundColor Yellow
    }
}

}
catch {
    Stop-Publish -Title 'Publish failed: unexpected error' -Reason $_.Exception.Message
}
