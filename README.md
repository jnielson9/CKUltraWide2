# About
Ultrawide2 support for Core Keeper, with full **32:9** rendering on top of the original 21:9 / 16:10 support.

[Available at mod.io](https://mod.io/g/corekeeper/m/jnielson9-ultrawide2#description)

## Configuration

The first time the mod runs, it creates a config file at Core Keeper's Unity persistent data folder (**not** next to the installed mod):

```
%USERPROFILE%\AppData\LocalLow\Pugstorm\Core Keeper\UltraWide2_config.json
```

You can paste that path straight into File Explorer's address bar to jump there.

Default contents:

```json
{
    "mode": "safe",
    "safeMarginTiles": 4
}
```

| Setting | Values | Default | Notes |
|---|---|---|---|
| `mode` | `"safe"` or `"full"` | `"safe"` | `safe` keeps a streaming margin so terrain at the screen edges pops/skews less. `full` pushes the view to the absolute 32:9 ceiling for maximum horizontal real estate, at the cost of more pronounced edge artifacts during movement. |
| `safeMarginTiles` | `0` – `24` | `4` | Only used when `mode` is `safe`. Higher values give cleaner edges but more letterboxing on extremely wide displays. The engine streams a 64-tile-wide window around the player, so the world view ends up `64 - 2 * safeMarginTiles` tiles wide. |

Edit the file, restart the game, and the new settings take effect. If anything goes wrong reading the file (bad JSON, etc.) the mod logs a warning and falls back to defaults.

> Note: even at higher safe-margin values some edge shimmer can remain during fast movement. That part appears to be inherent to how the game streams and updates terrain at runtime, and isn't something this mod can fully eliminate.

# Contribution
To work on the mod from the CoreKeeperModSDK, add this project as a submodule\clone into `CoreKeeperModSDK/Assets/UltraWide`:

```
git submodule add https://github.com/jnielson9/CKUltraWide2.git .\Assets\UltraWide
```
