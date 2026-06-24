# CI compliance checklist (public)

Quick pass before merging larger changes. CI runs on pull requests (and manually via Actions); Release is manual only. Checks use `.github/scripts/check-rules.ps1` and `.github/compliance/policy.json`.

Run locally from the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .github\scripts\check-rules.ps1
```

Items below are **not** fully automated; review them in code review.

## Security and keys

- [ ] No Steam Web API keys, tokens, passwords, or private material committed in source, samples, or screenshots.

## Threading and async

- [ ] Control updates run on the UI thread after `await` or `Task.Run` (`Invoke` / `BeginInvoke` when required).
- [ ] No new UI-thread deadlocks from `.Result` / `Wait()` / `GetAwaiter().GetResult()` on the WinForms sync context without a documented exception.
- [ ] Discarded tasks use `ForgetFaults` (or another explicit fault path).

## Layers

- [ ] New business rules, Steam/web I/O, and Goldberg file writes live in `Services/` or `Generators/`, not duplicated in `Forms/`.

## Paths and contracts

- [ ] New stable path segments use `PathConstants` (or existing central helpers), not scattered literals in `Forms/`.
- [ ] Changes to generated `steam_settings` or Goldberg layout match emulator expectations; player-visible layout changes update root `README.md` when applicable.

## Project constraints

- [ ] C# stays within **7.3** and **.NET Framework 4.8** unless `SmartGoldbergEmu.csproj` is intentionally upgraded.
- [ ] JSON uses in-repo **JsonKit** unless the project is explicitly migrating serializers.

## Resources and UI

- [ ] New or touched disposable fields (images, streams, timers, subscriptions) have dispose or unsubscribe paths.

## Repository hygiene

- [ ] No intentional commits under `bin/`, `obj/`, `.vs/`, or `tools/`.
- [ ] Private `docs/` and `.cursor/` stay local (`.gitignore`); remove from git if they were ever committed.
- [ ] Agent or automation did not commit or push on your behalf unless you explicitly asked.

## Goldberg launch (product boundary)

- [ ] No launcher-side injection (`ColdClientLoader`, `steamclient_loader`, cross-process inject APIs). Deploy uses copy/replace, registry, `load_dlls` staging, and `Process.Start` only.
