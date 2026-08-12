# NPC Portrait Customizer

Customize NPC portraits and your player character anytime in-game for Tale of Immortals (鬼谷八荒)!

![NPC Portrait Customizer Preview](https://raw.githubusercontent.com/saikanyas/NpcPortraitCustomizer/main/asset/NpcPortraitCustomizePVGif.gif)

## Requirements

- [MelonLoader](https://github.com/LavaGang/MelonLoader) (v0.5.7+ recommended, compatible with v0.4.0+)

## Key Features

- **Customize NPCs:** Click the "Customize" button on any NPC profile screen.
- **Customize Your Character:** Press F9 anywhere in-game to change your character's appearance.
- **Full Customization:** Modify faces, hairstyles, outfits, and accessories while preserving NPC traits and stats.
- **State Preservation:** Keeps untouched NPC features intact using active feature tracking.

## Installation

1. Install MelonLoader for Tale of Immortals.
2. Download the latest `NPCPortraitCustomizer.dll` from [Releases](https://github.com/saikanyas/NpcPortraitCustomizer/releases).
3. Copy `NPCPortraitCustomizer.dll` into your game's `Mods/` directory.

## How to Use

- **NPC:** Open an NPC's profile screen → Click "Customize".
- **Player:** Press F9 anywhere in-game.

## Building from Source

1. Clone this repository:
   ```bash
   git clone https://github.com/saikanyas/NpcPortraitCustomizer.git
   ```
2. Place game reference assemblies in `ModCode/ModMain/libs/`.
3. Build using .NET SDK:
   ```bash
   dotnet build ModCode/ModMain/ModMain.csproj -c Release
   ```

## License

Distributed under the MIT License. See [`LICENSE`](LICENSE) for more information.