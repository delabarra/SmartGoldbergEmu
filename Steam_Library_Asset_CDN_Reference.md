# Steam Library Asset CDN URLs

## Overview

If you already have the **asset hash** (or the asset paths from Steam's VDF/API), then constructing the CDN URL is straightforward.

Current CDN base:

```
https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appid}/
```

General format:

```
https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appid}/{hash}/{filename}
```

## Library Asset Filenames

```
library_capsule.jpg
library_600x900.jpg
library_600x900_2x.jpg
library_hero.jpg
library_hero_2x.jpg
library_hero_blur.jpg
library_header.jpg
library_header_2x.jpg
logo.png
logo_2x.png
```

## Important: The Hash Is NOT Universal

Each asset type may have its own hash/path.

Example:

```json
{
  "library_capsule": "37ca.../library_capsule.jpg",
  "library_hero": "91ff.../library_hero.jpg",
  "logo": "c817.../logo.png"
}
```

Build URLs by prepending:

```
https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appid}/
```

Result:

```
https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/570/37ca.../library_capsule.jpg
```

## Legacy CDN

```
https://steamcdn-a.akamaihd.net/steam/apps/{appid}/
```

Still works for many older games but is unreliable for newer titles.

## Summary

- Base:
  `https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appid}/`
- Format:
  `https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appid}/{hash}/{filename}`
- Don't reuse hashes across asset types unless Steam returns the same path.
