# Public CI compliance

This folder is the **source of truth for automated checks** in GitHub Actions. It does not depend on private `docs/` or `.cursor/` trees.

| File | Role |
|------|------|
| `policy.json` | Machine-readable rules for `check-rules.ps1` |
| `CHECKLIST.md` | Manual review items (not all are automatable) |

Workflows: `.github/workflows/pr.yml` (compliance + build + tests), `.github/workflows/release.yml` and `.github/workflows/preview.yml` (manual publish).

## Local workflow (before a PR)

From the repo root:

```powershell
# Automated rules (same step as CI; runs on Linux in Actions, locally on any OS with pwsh)
pwsh -ExecutionPolicy Bypass -File .github/scripts/check-rules.ps1

# MSBuild Release (use your VS MSBuild if the path below fails)
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
  -latest -products * -requires Microsoft.Component.MSBuild `
  -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
& $msbuild SmartGoldbergEmu.csproj /t:Build /p:Configuration=Release /p:Platform=x64 /m /v:m /nologo
dotnet test Tests\Tests.csproj -c Release -p:Platform=x64 -v minimal
```

Then skim **`CHECKLIST.md`** for items the script cannot enforce (disposal, README for player-visible changes, no duplicated business logic in `Forms/`).

## How to apply rules day to day

- **Default:** Fix rule issues only in files you touch for a feature or bugfix; keep `check-rules.ps1` green.
- **While editing:** New paths → `PathConstants` / existing helpers; discarded `Task` → `ForgetFaults`; I/O and Goldberg writes → `Services/` or `Generators/`.
- **Optional cleanup PRs:** One theme at a time (for example path centralization in a single service, or reducing duplicated helpers)—avoid repo-wide `///` or sweeping refactors.

Private maintainer notes may live under local `docs/` or `.cursor/` (gitignored). For shared enforcement, extend **`policy.json`** and **`check-rules.ps1`**, not only local Cursor rules.
