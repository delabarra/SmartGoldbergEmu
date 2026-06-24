# Static compliance pass driven by .github/compliance/policy.json (no docs/ or .cursor/).
#
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File .github\scripts\check-rules.ps1
#   powershell -ExecutionPolicy Bypass -File .github\scripts\check-rules.ps1 -Verbose
#
# Exits 1 when any check fails. Manual items: .github/compliance/CHECKLIST.md

param(
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$policyPath = Join-Path $PSScriptRoot '..\compliance\policy.json'
Set-Location $repoRoot

if (-not (Test-Path -LiteralPath $policyPath)) {
    Write-Error "Missing CI policy file: $policyPath"
}

$policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json
$manualChecklist = Join-Path $repoRoot ($policy.manualChecklistPath -replace '/', '\')
if (-not (Test-Path -LiteralPath $manualChecklist)) {
    Write-Error "Missing manual checklist: $manualChecklist"
}

$failures = New-Object System.Collections.Generic.List[string]

function Add-Failure {
    param([string]$Message)
    $script:failures.Add($Message)
}

function Write-Check {
    param([string]$Name, [bool]$Passed, [string]$Detail = '')
    if ($Passed) {
        if ($Verbose) { Write-Host "[ok] $Name" -ForegroundColor Green }
    }
    else {
        Write-Host "[FAIL] $Name" -ForegroundColor Red
        if ($Detail) { Write-Host "       $Detail" -ForegroundColor Red }
    }
}

function Test-PathExcluded {
    param([string]$RelativePath, [string[]]$Segments)
    foreach ($seg in $Segments) {
        if ($RelativePath -match [regex]::Escape($seg)) { return $true }
    }
    return $false
}

function Get-RepoTextFiles {
    param([string[]]$Roots, [string[]]$Extensions, [string[]]$ExcludeSegments)
    $files = @()
    foreach ($root in $Roots) {
        $full = if ([IO.Path]::IsPathRooted($root)) { $root } else { Join-Path $repoRoot $root }
        if (-not (Test-Path -LiteralPath $full)) { continue }
        if ((Get-Item -LiteralPath $full).PSIsContainer) {
            $files += Get-ChildItem -LiteralPath $full -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { $Extensions -contains $_.Extension.ToLowerInvariant() }
        }
        elseif ($Extensions -contains ([IO.Path]::GetExtension($full).ToLowerInvariant())) {
            $files += Get-Item -LiteralPath $full
        }
    }
    return @($files | Where-Object {
            $rel = $_.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
            -not (Test-PathExcluded -RelativePath $rel -Segments $ExcludeSegments)
        } | Select-Object -ExpandProperty FullName -Unique)
}

function Test-CsprojCeiling {
    $rel = $policy.csproj.path
    $csproj = Join-Path $repoRoot ($rel -replace '/', '\')
    if (-not (Test-Path -LiteralPath $csproj)) {
        Add-Failure "$rel is missing."
        return
    }
    $xml = Get-Content -LiteralPath $csproj -Raw
    $fw = [string]$policy.csproj.targetFrameworkVersion
    $lang = [string]$policy.csproj.langVersion
    $okFramework = $xml -match "<TargetFrameworkVersion>$([regex]::Escape($fw))</TargetFrameworkVersion>"
    $okLang = $xml -match "<LangVersion>$([regex]::Escape($lang))</LangVersion>"
    if (-not $okFramework) { Add-Failure "$rel must contain <TargetFrameworkVersion>$fw</TargetFrameworkVersion>." }
    if (-not $okLang) { Add-Failure "$rel must contain <LangVersion>$lang</LangVersion>." }
    Write-Check -Name "C# $lang / .NET Framework $fw (csproj)" -Passed ($okFramework -and $okLang)
}

function Test-NoExternalJsonPackages {
    $patterns = @($policy.jsonPackagesForbidden)
    $searchRoots = @(
        (Join-Path $repoRoot 'SmartGoldbergEmu.csproj'),
        (Join-Path $repoRoot 'packages.config')
    )
    $bad = @()
    foreach ($path in $searchRoots) {
        if (-not (Test-Path -LiteralPath $path)) { continue }
        $text = Get-Content -LiteralPath $path -Raw
        foreach ($p in $patterns) {
            if ($text -match $p) { $bad += "$([IO.Path]::GetFileName($path)): $p" }
        }
    }
    if ($bad.Count -gt 0) {
        foreach ($b in $bad) { Add-Failure "External JSON package reference: $b" }
    }
    Write-Check -Name 'JsonKit only (no external JSON packages in manifest)' -Passed ($bad.Count -eq 0)
}

function Test-NoHardcodedSecrets {
    $secretPattern = '(?i)(api[_-]?key|steam.*web.*api.*key)\s*[=:]\s*[\x22\x27][A-Za-z0-9]{16,}[\x22\x27]'
    $roots = @($policy.secretScan.roots)
    $exts = @($policy.secretScan.extensions)
    $exclude = @($policy.secretScan.excludePathSegments)
    $hits = @()
    foreach ($file in (Get-RepoTextFiles -Roots $roots -Extensions $exts -ExcludeSegments $exclude)) {
        $rel = $file.Substring($repoRoot.Length).TrimStart('\', '/')
        $lines = Get-Content -LiteralPath $file -ErrorAction SilentlyContinue
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match $secretPattern) {
                $hits += "${rel}:$($i + 1)"
            }
        }
    }
    if ($hits.Count -gt 0) {
        Add-Failure "Possible hardcoded API key ($($hits.Count) hit(s)): $($hits -join ', ')"
    }
    Write-Check -Name 'No hardcoded Steam Web API keys in tracked source' -Passed ($hits.Count -eq 0) -Detail ($hits -join '; ')
}

function Test-NoLauncherInjectionInCs {
    $forbidden = @($policy.forbiddenInCs)
    $csFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -Filter '*.cs' -File |
        Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\' }
    $hits = @()
    foreach ($file in $csFiles) {
        $rel = $file.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
        $text = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($term in $forbidden) {
            if ($text -match [regex]::Escape($term)) {
                $hits += "${rel}:$term"
            }
        }
    }
    if ($hits.Count -gt 0) {
        Add-Failure "Forbidden launcher injection API/name in C# ($($hits.Count)): $($hits -join ', ')"
    }
    Write-Check -Name 'No launcher-side injection paths in *.cs' -Passed ($hits.Count -eq 0)
}

function Test-NoTrackedBuildArtifacts {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Check -Name 'No tracked bin/obj/.vs (git)' -Passed $true -Detail 'git not on PATH; skipped'
        return
    }
    $tracked = git -C $repoRoot ls-files 2>$null
    if (-not $tracked) {
        Write-Check -Name 'No tracked bin/obj/.vs (git)' -Passed $true -Detail 'not a git repo or no files; skipped'
        return
    }
    $patterns = @($policy.git.trackedArtifactSegments)
    $bad = $tracked | Where-Object {
        $path = $_
        foreach ($pat in $patterns) {
            if ($path -match $pat) { return $true }
        }
        return $false
    }
    if ($bad) {
        foreach ($b in $bad) { Add-Failure "Tracked build/IDE artifact: $b" }
    }
    Write-Check -Name 'No tracked bin/obj/.vs artifacts' -Passed (-not $bad)
}

function Test-MustNotTrackPaths {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Check -Name 'Private/local paths not tracked (git)' -Passed $true -Detail 'git not on PATH; skipped'
        return
    }
    $bad = New-Object System.Collections.Generic.List[string]
    foreach ($prefix in @($policy.git.mustNotTrackPrefixes)) {
        $tracked = @(git -C $repoRoot ls-files "${prefix}*" 2>$null)
        foreach ($t in $tracked) { $bad.Add($t) }
    }
    foreach ($file in @($policy.git.mustNotTrackFiles)) {
        $tracked = @(git -C $repoRoot ls-files $file 2>$null)
        foreach ($t in $tracked) { $bad.Add($t) }
    }
    if ($bad.Count -gt 0) {
        foreach ($b in $bad) { Add-Failure "Must not be published in git: $b" }
    }
    Write-Check -Name 'tools/ and publish-release.bat not tracked' -Passed ($bad.Count -eq 0) -Detail ($bad -join '; ')
}

function Test-UnitTestsProjectTracked {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Check -Name 'Tests project tracked in git' -Passed $true -Detail 'git not on PATH; skipped'
        return
    }
    $required = @($policy.git.mustTrackFiles)
    $missing = @()
    foreach ($rel in $required) {
        $tracked = @(git -C $repoRoot ls-files $rel 2>$null)
        if ($tracked.Count -eq 0) { $missing += $rel }
    }
    if ($missing.Count -gt 0) {
        foreach ($m in $missing) { Add-Failure "$m must be tracked (unit tests run in CI)." }
    }
    Write-Check -Name 'Required test project tracked in git' -Passed ($missing.Count -eq 0) -Detail ($required -join ', ')
}

function Test-SyncOverAsyncAllowlist {
    $cfg = $policy.formsSyncOverAsync
    $formsDir = Join-Path $repoRoot ($cfg.scanDirectory -replace '/', '\')
    if (-not (Test-Path -LiteralPath $formsDir)) { return }
    $pattern = [string]$cfg.linePattern
    $exclusions = @($cfg.lineExclusions)
    $hits = @()
    $files = Get-ChildItem -LiteralPath $formsDir -Recurse -Filter '*.cs' -File
    foreach ($file in $files) {
        $rel = $file.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
        $lines = Get-Content -LiteralPath $file.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if ($line -notmatch $pattern) { continue }
            $skip = $false
            foreach ($ex in $exclusions) {
                if ($line -match $ex) { $skip = $true; break }
            }
            if ($skip) { continue }
            $hits += "${rel}:$($i + 1)"
        }
    }
    if ($hits.Count -gt 0) {
        Add-Failure "Blocking wait/GetResult in Forms/ ($($hits.Count)): $($hits -join ', '). See $($policy.manualChecklistPath)."
    }
    Write-Check -Name 'No undocumented sync-over-async in Forms/' -Passed ($hits.Count -eq 0) -Detail ($hits -join '; ')
}

function Test-DiscardedTasksUseForgetFaults {
    $csFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -Filter '*.cs' -File |
        Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\' }
    $bad = @()
    foreach ($file in $csFiles) {
        $rel = $file.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
        $lines = Get-Content -LiteralPath $file.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -notmatch '^\s*_ = ') { continue }
            if ($lines[$i] -match 'ForgetFaults') { continue }
            if ($lines[$i] -match '^\s*_ = \s*new\s+') { continue }
            $foundForget = $false
            $j = $i
            $maxLine = [Math]::Min($i + 50, $lines.Count - 1)
            while ($j -le $maxLine) {
                if ($lines[$j] -match 'ForgetFaults') {
                    $foundForget = $true
                    break
                }
                $j++
            }
            if (-not $foundForget) {
                $bad += "${rel}:$($i + 1)"
            }
            else {
                $i = $j
            }
        }
    }
    if ($bad.Count -gt 0) {
        Add-Failure "Discarded task without ForgetFaults ($($bad.Count)): $($bad -join ', ')"
    }
    Write-Check -Name 'Discarded tasks use ForgetFaults' -Passed ($bad.Count -eq 0) -Detail ($bad -join '; ')
}

function Test-NoDuplicateServiceLocator {
    $csFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -Filter '*.cs' -File |
        Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\' }
    $declarations = @()
    foreach ($file in $csFiles) {
        $lines = Get-Content -LiteralPath $file.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match 'class\s+ServiceLocator\b') {
                $rel = $file.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
                $declarations += "${rel}:$($i + 1)"
            }
        }
    }
    $passed = $declarations.Count -le 1
    if (-not $passed) {
        Add-Failure "Multiple ServiceLocator type declarations: $($declarations -join ', ')"
    }
    Write-Check -Name 'Single ServiceLocator type' -Passed $passed
}

function Test-FormsGoldbergPathLiterals {
    $formsDir = Join-Path $repoRoot 'Forms'
    if (-not (Test-Path -LiteralPath $formsDir)) { return }
    $literalPattern = [string]$policy.formsPathLiteralPattern
    $hits = @()
    $files = Get-ChildItem -LiteralPath $formsDir -Recurse -Filter '*.cs' -File
    foreach ($file in $files) {
        if ($file.Name -like '*.Designer.cs') { continue }
        $rel = $file.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
        $lines = Get-Content -LiteralPath $file.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match $literalPattern) {
                $hits += "${rel}:$($i + 1)"
            }
        }
    }
    if ($hits.Count -gt 0) {
        Add-Failure "Path literals in Forms/ (use PathConstants): $($hits -join ', ')"
    }
    Write-Check -Name 'No games/goldberg/steam_settings path literals in Forms/' -Passed ($hits.Count -eq 0) -Detail ($hits -join '; ')
}

Write-Host "SmartGoldbergEmu compliance check (policy: .github/compliance/policy.json)" -ForegroundColor Cyan

Test-CsprojCeiling
Test-NoExternalJsonPackages
Test-NoHardcodedSecrets
Test-NoLauncherInjectionInCs
Test-NoTrackedBuildArtifacts
Test-MustNotTrackPaths
Test-UnitTestsProjectTracked
Test-SyncOverAsyncAllowlist
Test-DiscardedTasksUseForgetFaults
Test-NoDuplicateServiceLocator
Test-FormsGoldbergPathLiterals

if ($failures.Count -eq 0) {
    Write-Host ''
    Write-Host 'All automated compliance checks passed.' -ForegroundColor Green
    exit 0
}

Write-Host ''
Write-Host "$($failures.Count) check(s) failed:" -ForegroundColor Red
foreach ($f in $failures) {
    Write-Host "  - $f" -ForegroundColor Red
}
Write-Host ''
Write-Host "Manual review: $($policy.manualChecklistPath)" -ForegroundColor Yellow
exit 1
