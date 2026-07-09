# Steam Client Package CDN

How to download Steam client UI files (for example `steamui/sounds/desktop_toast_default.wav`) from Valve's CDN without using the SteamPipe depot/chunk pipeline.

This is **not** the same system used for game depots. Client UI assets ship as versioned packages under `/client/`, not as static store/community URLs and not as anonymously downloadable App 7 depots.

---

## TL;DR

| Question | Answer |
|---|---|
| Direct static URL to a client UI file? | **No** — paths like `/steamui/sounds/foo.wav` return 404 on store CDNs |
| SteamPipe depot download (`/depot/{id}/chunk/{sha}`)? | **No** — client UI is not exposed that way for App 7 |
| Correct download path | Fetch `steam_client_win64` manifest → read package `zipvz` → download from `/client/{zipvz}` → decompress `.vz` → unzip → extract file |

**Working example (current as of investigation on 2026-07-09):**

```
Manifest:  https://cdn.cloudflare.steamstatic.com/client/steam_client_win64
Package:   https://cdn.cloudflare.steamstatic.com/client/steamui_websrc_sounds_all.zip.vz.5ba6acd8f4dfe4b93437895b20344fab5bb3ff96_3714779
Inner path: steamui/sounds/desktop_toast_default.wav
```

---

## Background: two different CDN systems

Steam uses at least two distinct content delivery models:

### 1. Store / community static assets

Used for game store images, community icons, etc.

```
https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appId}/{filename}
https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/{appId}/{hash}.jpg
```

These are plain HTTP GETs. No manifest, no decryption, no chunks.

### 2. SteamPipe depots (games, tools, redistributables)

Used for game installs. Requires:

1. CM login (anonymous works for many public depots)
2. Resolve app → depot → manifest GID from PICS/AppInfo
3. Depot decryption key from CM
4. Manifest request code (for some manifests)
5. Download manifest from CDN
6. Download chunk(s) via `/depot/{depotId}/chunk/{sha}?t=...`
7. Decrypt, decompress, reassemble locally

Tools: [DepotDownloader](https://github.com/SteamRE/DepotDownloader), SteamKit-based libraries.

### 3. Client packages (Steam client itself)

Used for the Steam desktop client's own files: `steamui/`, `public/`, binaries, strings, sounds, etc.

- Listed in `{SteamInstall}/package/steam_client_win64.manifest` (or `steam_client_win32.manifest`, `steam_client_ubuntu12`, etc.)
- Package blobs live under `{SteamInstall}/package/` as `*.zip.vz.{hash}_{size}` files
- Downloaded from **`/client/`** URLs, not `/steamui/...` and not `/depot/...`

The Steam client updater fetches a manifest, compares versions, then downloads changed packages. This is the same channel you can use to fetch client UI assets programmatically.

---

## What does *not* work

### Static mirrors (404)

Tested and failed:

```
https://cdn.cloudflare.steamstatic.com/steamui/sounds/desktop_toast_default.wav
https://shared.fastly.steamstatic.com/steamui/sounds/desktop_toast_default.wav
https://steamcdn-a.akamaihd.net/steamui/sounds/desktop_toast_default.wav
```

Client UI files are not published as direct static paths on store/community CDNs.

### SteamPipe depot URLs (403 / wrong shape)

```
https://{cdn}/depot/{depotId}/chunk/{sha}
```

That URL shape is for depot chunks. Client UI packages are not served there.

### App 7 via DepotDownloader (anonymous)

```
DepotDownloader -app 7 -manifest-only
```

With anonymous login, App 7 returns **no depots**. Probing common depot IDs (2, 228988, etc.) yields `Depot X not listed for app 7`. Even if a depot existed, the client UI sounds live in the **package** system, not in those depots.

---

## Client manifest

### Location on disk

```
{SteamInstall}/package/steam_client_win64.manifest   # Windows 64-bit
{SteamInstall}/package/steam_client_win32.manifest   # Windows 32-bit
{SteamInstall}/package/steam_client_ubuntu12.manifest
```

### CDN mirror

```
https://cdn.steamstatic.com/client/steam_client_win64
https://client-download.steampowered.com/client/steam_client_win64
```

Alternate hosts (tested 2026-07-09):

| Host | `/client/steam_client_win64` | `/client/{package.zip.vz...}` |
|---|---|---|
| `cdn.steamstatic.com` | 200 | 200 |
| `client-download.steampowered.com` | 200 | 200 |
| `cdn.cloudflare.steamstatic.com` | 200 | 200 |
| `steamcdn-a.akamaihd.net` | 200 | 200 |
| `shared.fastly.steamstatic.com` | 404 | 404 |
| `shared.cloudflare.steamstatic.com` | 301 → Cloudflare | — |

Prefer `cdn.cloudflare.steamstatic.com` or `steamcdn-a.akamaihd.net` if you need hosts that overlap with store CDN allowlists. Fastly's `shared.*` mirror does **not** carry `/client/` content.

### Manifest format (VDF)

Each package entry has:

| Field | Meaning |
|---|---|
| `file` | Uncompressed zip filename on disk after install, e.g. `steamui_websrc_sounds_all.zip.{git-hash}` |
| `size` | Uncompressed zip size in bytes |
| `sha2` | SHA-256 of the uncompressed zip |
| `zipvz` | Compressed download filename (`.vz` blob) |
| `sha2vz` | SHA-256 of the `.vz` download blob |

Example excerpt (`steam_client_win64.manifest`):

```vdf
"steamui_websrc_sounds_all"
{
    "file"   "steamui_websrc_sounds_all.zip.74727ca40ef88c5012bb631488fd285418dce825"
    "size"   "4676203"
    "sha2"   "c823bd39d234e7d9a5fdb5ec6605eae3f175e52bb847725e8826ff526a61641c"
    "zipvz"  "steamui_websrc_sounds_all.zip.vz.5ba6acd8f4dfe4b93437895b20344fab5bb3ff96_3714779"
    "sha2vz" "ac62e3d0e6213bcbdcfe973c624eda0486977dccdc4098c10b60b62e894ea93b"
}
```

The `zipvz` filename embeds the download size after the underscore (`3714779` bytes). Verify with `sha2vz` after download.

### Relevant packages for `steamui/`

| Package key | Contents (approximate) |
|---|---|
| `steamui_websrc_all` | Minified Steam UI JavaScript, HTML, CSS |
| `steamui_websrc_sounds_all` | `steamui/sounds/*.wav`, `.m4a` |
| `steamui_websrc_movies_all` | UI video assets |
| `public_all` | Public client files, string tables |
| `strings_all` / `strings_en_all` | Localized strings |
| `resources_all` | Client resources |
| `bins_win64` / `bins_*` | Binaries |

For notification sounds, use **`steamui_websrc_sounds_all`**.

---

## Download URL shape

```
https://{host}/client/{zipvz filename}
```

Example:

```
https://cdn.cloudflare.steamstatic.com/client/steamui_websrc_sounds_all.zip.vz.5ba6acd8f4dfe4b93437895b20344fab5bb3ff96_3714779
```

No authentication token is required for current public client packages. Plain HTTP GET.

---

## `.vz` decompression

Client packages use Valve's **VZip** format (legacy LZMA), not raw zlib.

### File layout

```
┌─────────────────────────────────────────┐
│ Header: "VZa" (3 bytes) + timestamp (4) │  7 bytes total
├─────────────────────────────────────────┤
│ LZMA properties (5 bytes)               │
├─────────────────────────────────────────┤
│ LZMA-compressed payload                   │
├─────────────────────────────────────────┤
│ CRC32 of decompressed data (4 bytes)     │
│ Decompressed size (4 bytes)              │
│ Footer: "zv" (2 bytes)                   │  10 bytes total
└─────────────────────────────────────────┘
```

Newer depot chunks may use **VSZa** (Zstandard) instead; client packages investigated here used **VZa**.

### Python reference (verified)

```python
import struct, zlib, lzma, zipfile, io

def decompress_vzip(data: bytes) -> bytes:
    assert data[:3] == b"VZa"
    off = 7
    props = data[off : off + 5]
    off += 5
    crc, size = struct.unpack("<II", data[-10:-2])
    assert data[-2:] == b"zv"
    comp = data[off:-10]
    p = props[0]
    filters = [{"id": lzma.FILTER_LZMA1, "lc": p % 9, "lp": (p // 9) % 5, "pb": p // 45}]
    raw = lzma.decompress(comp, format=lzma.FORMAT_RAW, filters=filters)
    if len(raw) != size:
        raise ValueError(f"size mismatch: got {len(raw)}, expected {size}")
    if zlib.crc32(raw) & 0xFFFFFFFF != crc:
        raise ValueError("CRC mismatch")
    return raw

# raw is a zip archive
with zipfile.ZipFile(io.BytesIO(raw)) as zf:
    wav = zf.read("steamui/sounds/desktop_toast_default.wav")
```

### Existing tools

- [yaakov-h/vunzip](https://github.com/yaakov-h/vunzip) — C CLI for `.vz`
- [johndrinkwater's Python gist](https://gist.github.com/johndrinkwater/8944787) — reference implementation
- [node-steam-user `unzip`](https://github.com/DoctorMcKay/node-steam-user) — handles VZa, VSZa, and plain zip

---

## End-to-end workflow

```
1. GET /client/steam_client_win64
        ↓
2. Parse VDF → find package key (e.g. steamui_websrc_sounds_all)
        ↓
3. Read zipvz + sha2vz
        ↓
4. GET /client/{zipvz}
        ↓
5. Verify SHA-256 == sha2vz
        ↓
6. Decompress .vz (VZa/LZMA) → inner .zip
        ↓
7. Unzip → extract target path (e.g. steamui/sounds/desktop_toast_default.wav)
        ↓
8. Optionally verify file hash against local install or manifest sha2 of inner zip
```

### Pseudocode

```text
manifest = http_get("https://cdn.cloudflare.steamstatic.com/client/steam_client_win64")
pkg = vdf_lookup(manifest, "steamui_websrc_sounds_all")
blob = http_get(f"https://cdn.cloudflare.steamstatic.com/client/{pkg.zipvz}")
assert sha256(blob) == pkg.sha2vz
zip_bytes = decompress_vzip(blob)
file_bytes = zip_extract(zip_bytes, "steamui/sounds/desktop_toast_default.wav")
```

---

## Worked example: `desktop_toast_default.wav`

Steam desktop notification toast sound. Plays occasionally instead of `deck_ui_toast.wav` on desktop mode.

| Property | Value |
|---|---|
| Installed path | `{SteamInstall}/steamui/sounds/desktop_toast_default.wav` |
| Package | `steamui_websrc_sounds_all` |
| Inner zip path | `steamui/sounds/desktop_toast_default.wav` |
| Size | 111,952 bytes |
| SHA-1 | `61b9d5cd0757b14df895916481cdf2bcb56660f7` |
| Package `zipvz` (2026-07-09) | `steamui_websrc_sounds_all.zip.vz.5ba6acd8f4dfe4b93437895b20344fab5bb3ff96_3714779` |
| Package `sha2vz` | `ac62e3d0e6213bcbdcfe973c624eda0486977dccdc4098c10b60b62e894ea93b` |

Other sounds in the same package include `deck_ui_toast.wav`, `desktop_toast_short.wav`, `steam_os_startup.wav`, chat notification `.m4a` files, etc.

### Version drift

The `zipvz` filename changes whenever Valve updates the sounds package. **Do not hardcode** the full URL in production; always resolve via the client manifest.

`manifest.version` is a monotonic build number (e.g. `1782866176`); use it for cache invalidation.

---

## Comparison table

| Aspect | Store/community CDN | SteamPipe depot | Client package CDN |
|---|---|---|---|
| URL prefix | `/store_item_assets/`, `/steamcommunity/` | `/depot/{id}/chunk/` or `/depot/{id}/manifest/` | `/client/` |
| Auth | None | Often none for public depots; manifest codes for some | None for current packages |
| Format | Raw file | Encrypted chunks + manifest | `.vz` → zip → files |
| App ID | Per-game (`{appId}` in path) | Per-game depot | Implicit (Steam client) |
| Anonymous access | Yes | Usually for public games | Yes |
| Example file | `header.jpg` for app 570 | `csgo/maps/de_dust2.bsp` | `steamui/sounds/desktop_toast_default.wav` |

---

## Implementation notes

### Caching

- Cache the parsed manifest keyed by `version`.
- Cache decompressed packages keyed by `sha2vz` (the download blob hash).
- Individual files inside the zip can be indexed by path without re-downloading until `sha2vz` changes.

### Platform selection

Use the manifest matching the target platform:

| Platform | Manifest name |
|---|---|
| Windows 64-bit | `steam_client_win64` |
| Windows 32-bit | `steam_client_win32` |
| Linux | `steam_client_ubuntu12` |
| macOS | `steam_client_osx` |

Package keys are largely shared, but `zipvz` hashes can diverge per platform manifest.

### Error handling

- **404 on `/client/`** — wrong host (try Cloudflare/Akamai, not Fastly `shared.*`) or stale `zipvz` filename.
- **CRC/size mismatch after decompress** — truncated download or wrong decompressor (check VZa vs VSZa magic).
- **File missing in zip** — wrong package key; sounds are in `steamui_websrc_sounds_all`, not `steamui_websrc_all`.

### Legal / ToS

This documents publicly reachable client update URLs the Steam installer already uses. Use responsibly; respect Valve's terms of service for redistribution.

---

## References

- [Jonius7/SteamUI-OldGlory — Steam Folder Tidbits](https://github.com/Jonius7/SteamUI-OldGlory/wiki/Steam-Folder-Tidbits) — early documentation of `/client/` URLs
- [SteamRE/DepotDownloader](https://github.com/SteamRE/DepotDownloader) — depot pipeline (not for client packages)
- [yaakov-h/vunzip](https://github.com/yaakov-h/vunzip) — VZip format details
- [datkat21 — Steam Deck UI sounds](https://gist.github.com/datkat21/953d91cf9657c46f296ea991ea50ed2c) — sound file inventory under `steamui/sounds/`
- [EliCunninghamDev/steamdepot — SteamProtocolBreakdown.md](https://github.com/EliCunninghamDev/steamdepot/blob/main/SteamProtocolBreakdown.md) — depot/chunk pipeline (contrast with client packages)

---

## Investigation log

Verified on Windows 10, Steam install at `C:\Program Files (x86)\Steam`, 2026-07-09:

1. Local file exists at `steamui/sounds/desktop_toast_default.wav` (111,952 bytes).
2. Static CDN paths return 404.
3. DepotDownloader anonymous App 7 returns no depots.
4. `steam_client_win64.manifest` lists `steamui_websrc_sounds_all` with `zipvz` blob.
5. Download from `cdn.steamstatic.com/client/{zipvz}` — HTTP 200, SHA-256 matches `sha2vz`.
6. VZa decompress → zip → `steamui/sounds/desktop_toast_default.wav` — SHA-1 matches local install.
