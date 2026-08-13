# SmartGoldbergEmu

Streamlines and automates the configuration process for the Goldberg Emulator and launches games properly without modifying game files.

- 🚩 This is a fork of Kola124's [https://github.com/Kola124/SmartGoldbergEmu](https://github.com/Kola124/SmartGoldbergEmu)


## Features

- **Goldberg Emulator Management**: Download, install, and update Goldberg Emulator files ([Detanup01/gbe_fork](https://github.com/Detanup01/gbe_fork) and [alex47exe/gse_fork](https://github.com/alex47exe/gse_fork)).
- **Game Lookup**: Find games using either the App ID or game name.
- **steam_settings generation**: Automatically create Goldberg configuration files (including achievements, items, and stats).
- **Goldberg launch modes**: Support for **Steam client**, **Experimental**, **Steam.dll**, and **No emulation**.
- **Library Management**: Organize games and configure launch settings.
- **Per-Game Launch Options**: Customize launch arguments individually for each game.
- **User Profiles**: Create and switch between profiles (Steam ID and save data only).
- **SteamStub detection**: Detect and remove SteamStub manually or automatically.


## Quick start

1. Download the [latest release](https://github.com/delabarra/SmartGoldbergEmu/releases).
2. Launch SmartGoldbergEmu.exe (allow updates if prompted).
3. Add one or more games to your library.
4. Configure any desired launch options or emulator settings.
5. Start your game directly from SmartGoldbergEmu.


## Requirements

- 64-bit Windows
- .NET Framework 4.8
- [Steam Web API key](https://steamcommunity.com/dev/apikey) (optional).


## Dependencies

- Goldberg forks:
  - Detanup01/gbe_fork: [https://github.com/Detanup01/gbe_fork](https://github.com/Detanup01/gbe_fork)
  - alex47exe/gse_fork: [https://github.com/alex47exe/gse_fork](https://github.com/alex47exe/gse_fork)

