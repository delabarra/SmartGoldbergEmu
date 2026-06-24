# SmartGoldbergEmu

Streamlines and automates the configuration process for the Goldberg Emulator and launches games properly without the need of modifying game files.

- 🚩 This is a fork of Kola124´s [https://github.com/Kola124/SmartGoldbergEmu](https://github.com/Kola124/SmartGoldbergEmu)

## Features

- **Goldberg Emulator Management**: Download, install, and update Goldberg Emulator files. ([Detanup01/gbe_fork](https://github.com/Detanup01/gbe_fork) and [alex47exe/gse_fork](https://github.com/alex47exe/gse_fork)).
- **Library Management**: Organize games and configure launch settings.
- **Per-Game Launch Options**: Customize launch arguments individually for each game.
- **Goldberg launch modes**:  **Steam client**, **Experimental**, **Steam.dll**, and **No emulation**.
- **User Profiles**: Create and switch between profiles (SteamID and save data only).
- **App ID / Game Lookup**: Find games using either the App ID or game name.
- **steam_settings generation**: Automatically create Goldberg configuration files. Achievements, items, and stats require a Steam Web API key.
- **Steamless Integration (Optional)**: Support for [Steamless](https://github.com/atom0s/Steamless) by providing the installation path to `Steamless.CLI.exe`.

## Quick start

1. Download the [latest release](https://github.com/delabarra/SmartGoldbergEmu/releases).
2. Launch SmartGoldbergEmu.exe (allow updates if prompted).
3. Add one or more games to your library.
4. Configure any desired launch options or emulator settings.
5. Start your game directly from SmartGoldbergEmu.

## Requirements

- 64-bit Windows
- .NET Framework 4.8
- [Steam Web API key](https://steamcommunity.com/dev/apikey) (optional, required for achievements/items/stats)

## Dependencies

- SteamKit: [https://github.com/SteamRE/SteamKit](https://github.com/SteamRE/SteamKit)
- Goldberg repack (primary download): [delabarra/GoldbergEmu-Forks-Repacked](https://github.com/delabarra/GoldbergEmu-Forks-Repacked)
- Goldberg forks (fallback): [Detanup01/gbe_fork](https://github.com/Detanup01/gbe_fork), [alex47exe/gse_fork](https://github.com/alex47exe/gse_fork)

