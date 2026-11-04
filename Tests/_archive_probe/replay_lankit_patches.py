import json
import os
from pathlib import Path

workspace = Path(r"h:\GitHub-Desktop\SmartGoldbergEmu")
root = Path(r"C:\Users\Lab\.cursor\projects\h-GitHub-Desktop-SmartGoldbergEmu\agent-transcripts")

relevant_suffixes = (
    "GoldbergFilesService.cs",
    "LanPresenceService.cs",
    "LanPlayersForm.cs",
    "MainForm.cs",
    "PathConstants.cs",
    "ServiceLocator.cs",
    "SmartGoldbergEmu.csproj",
    "LanGameInviteEventArgs.cs",
    "LanGameJoinService.cs",
)

jsonls = sorted(root.rglob("*.jsonl"), key=lambda p: p.stat().st_mtime)
ops = []
for jp in jsonls:
    with jp.open("r", encoding="utf-8") as f:
        for line in f:
            try:
                o = json.loads(line)
            except Exception:
                continue
            content = (o.get("message") or {}).get("content")
            if not isinstance(content, list):
                continue
            for part in content:
                if not isinstance(part, dict) or part.get("type") != "tool_use":
                    continue
                name = part.get("name")
                inp = part.get("input") or {}
                if name not in ("Write", "StrReplace"):
                    continue
                path = inp.get("path") or ""
                if not any(s in path.replace("\\", "/") for s in relevant_suffixes):
                    continue
                blob = json.dumps(inp)
                if path.endswith(
                    (
                        "MainForm.cs",
                        "LanPlayersForm.cs",
                        "LanPresenceService.cs",
                        "PathConstants.cs",
                        "GoldbergFilesService.cs",
                        "ServiceLocator.cs",
                        "SmartGoldbergEmu.csproj",
                    )
                ):
                    if not any(
                        k in blob
                        for k in (
                            "Invite",
                            "auto_send",
                            "LanGame",
                            "LanKit",
                            "Join game",
                            "InviteReceived",
                            "EnsureSteamId",
                            "LanLobby",
                            "lobby_connect",
                            "auto_accept",
                        )
                    ):
                        if "LanKit" not in blob and "LanGameJoin" not in blob:
                            continue
                ops.append((name, path, inp))

print("ops to apply", len(ops))

cache = {}


def load(path):
    p = Path(path)
    if not p.is_absolute():
        p = workspace / path
    key = str(p)
    if key not in cache:
        cache[key] = p.read_text(encoding="utf-8") if p.exists() else None
    return key, cache[key]


applied = 0
failed = []
for name, path, inp in ops:
    key, cur = load(path)
    if name == "Write":
        cache[key] = inp.get("contents") or ""
        applied += 1
        continue
    if cur is None:
        failed.append(("missing", path))
        continue
    old = inp.get("old_string") or ""
    new = inp.get("new_string") or ""
    replace_all = bool(inp.get("replace_all"))
    if old not in cur:
        failed.append(("nomatch", path, old[:100].replace("\n", " ")))
        continue
    cache[key] = cur.replace(old, new) if replace_all else cur.replace(old, new, 1)
    applied += 1

written = []
for key, content in cache.items():
    if content is None:
        continue
    p = Path(key)
    disk = p.read_text(encoding="utf-8") if p.exists() else None
    if disk == content:
        continue
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content, encoding="utf-8", newline="\n")
    written.append(str(p))

print("applied", applied, "failed", len(failed), "written", len(written))
for w in written:
    print("W", w)
print("--- failed ---")
for item in failed[:25]:
    print(item)
