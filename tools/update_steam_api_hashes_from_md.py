# One-shot: rebuild Windows steam_api / steam_api64 hash sets from steam_api_table.md
# Usage: python tools/update_steam_api_hashes_from_md.py

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MD_PATH = ROOT / "steam_api_table.md"
CS_PATH = ROOT / "Constants" / "SteamApiHashes.cs"

GRIM_DAWN_X32 = ("f1a6461f29152198c280fa1d85714d2c31b82e1a8148d92bd8c62c9f781a4fa7", "Grim Dawn")
GRIM_DAWN_X64 = ("8de54d32508e216c9135b8bf025749243d44e404c1c22a8e5fe35acecabe7a9c", "Grim Dawn")


def parse_section(md, title):
    m = re.search(
        rf"(?ms)^##\s+{re.escape(title)}\s*\n\n\|[^\n]+\n\|[^\n]+\n(.*?)(?=^##\s|\Z)",
        md,
    )
    if not m:
        raise SystemExit("section missing: " + title)
    by_hash = {}
    for line in m.group(1).splitlines():
        mm = re.match(
            r"\|\s*([^|]+?)\s*\|\s*([^|]*?)\s*\|\s*([0-9a-fA-F]{64})\s*\|",
            line,
        )
        if not mm:
            continue
        ver = mm.group(1).strip()
        h = mm.group(3).strip().lower()
        by_hash.setdefault(h, [])
        if ver not in by_hash[h]:
            by_hash[h].append(ver)
    return by_hash


def ver_key(v):
    m = re.match(r"^(\d+)([a-z]?)$", v, re.I)
    if not m:
        return (0, "")
    return (int(m.group(1)), m.group(2).lower())


def format_entries(by_hash, extras):
    items = []
    for h, vers in by_hash.items():
        vers_sorted = sorted(vers, key=ver_key, reverse=True)
        best = max(ver_key(v)[0] for v in vers)
        items.append((best, h, ", ".join(vers_sorted)))
    items.sort(key=lambda t: (t[0], t[1]))
    lines = ['                "{0}",//{1}'.format(h, comment) for _, h, comment in items]
    existing = set(by_hash)
    for h, comment in extras:
        if h not in existing:
            lines.append('                "{0}",//{1}'.format(h, comment))
    return "\n".join(lines)


def replace_hashset(src, key, body):
    pattern = (
        r'(\["' + re.escape(key) + r'"\]\s*=\s*new\s+HashSet<string>\([^)]*\)\s*\{)'
        r"\s*.*?"
        r"(\n\s*\})"
    )
    m = re.search(pattern, src, flags=re.S)
    if not m:
        raise SystemExit("hashset not found: " + key)
    return src[: m.start(1)] + m.group(1) + "\n" + body + m.group(2) + src[m.end(2) :]


def main():
    md = MD_PATH.read_text(encoding="utf-8-sig")
    cs = CS_PATH.read_text(encoding="utf-8")

    x32 = parse_section(md, "steam_api.dll")
    x64 = parse_section(md, "steam_api64.dll")
    print("parsed steam_api.dll={0} steam_api64.dll={1}".format(len(x32), len(x64)))

    block32 = format_entries(x32, [GRIM_DAWN_X32])
    block64 = format_entries(x64, [GRIM_DAWN_X64])

    new_cs = replace_hashset(cs, "steam_api.dll", block32)
    new_cs = replace_hashset(new_cs, "steam_api64.dll", block64)
    CS_PATH.write_text(new_cs, encoding="utf-8", newline="\r\n")
    print("updated " + str(CS_PATH))


if __name__ == "__main__":
    main()
