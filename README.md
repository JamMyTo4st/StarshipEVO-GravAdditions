# GravLevel for Starship EVO

A small BepInEx 5 plugin that auto-levels whatever you're holding with the **anti-grav tool** (grav gun) in **Starship EVO**.

> ✅ **Tested in-game and working (v1.0.0, game version 26w43a, November 2026). Game updates may break it, so back up your saves first.**
>
> ⚠️ **v1.1.0 (the face-player key) is NOT yet tested in-game.**

## What it does

Grab an object with the anti-grav tool, hold **B**, and the object turns upright to match the planet's gravity. Its heading also snaps to the nearest 90°. This makes it easy to set down crates, hoverbikes and small ships straight.

Hold **N** instead and the object levels AND turns so its front points toward you. For crafts with a yoke, the front is the pilot's forward. If the front is a different side of your build, change `FaceDirection`.

- **Up direction:** the opposite of planet gravity at the object's position. In deep space, with no planet gravity, it levels to your camera's up instead.
- **Physics-safe:** it only adds turning force (torque) through the game's own rigidbody API. It never sets rotation directly or teleports anything.
- **Vanilla controls still work:** R and Shift+R still change grab distance.

**Tip:** grabbing the object's center-of-mass sphere levels it most cleanly. If you grab a block off-center, the normal grab pull can fight the leveling.

## Controls

| Input | Action |
|---|---|
| Hold left mouse (anti-grav tool) | Grab an object (vanilla) |
| Hold **B** while grabbing | Level the object |
| Hold **N** while grabbing | Level the object and turn its front toward you |
| R / Shift+R | Change grab distance (vanilla) |

## Requirements

- Windows
- Starship EVO on Steam (app 711980)
- [BepInEx 5.4.x **x64**](https://github.com/BepInEx/BepInEx/releases) (for example `BepInEx_win_x64_5.4.23.x.zip`). BepInEx 6 is not supported.

## Install

1. In Steam, right-click **Starship EVO** > **Manage** > **Browse local files**. This opens the game folder (the one with `Starship EVO.exe`).
2. If you don't have BepInEx yet, extract the BepInEx 5 x64 zip into that folder. `BepInEx\`, `winhttp.dll` and `doorstop_config.ini` must end up next to `Starship EVO.exe`. Start the game once, wait for the main menu, then quit.
3. Download `GravLevel-v1.1.0.zip` from this repo's [Releases](../../releases) page and extract it into the game folder. You should then have `BepInEx\plugins\GravLevel\GravLevel.dll`.
4. Start the game.

To check it loaded, open `BepInEx\LogOutput.log` and look for `GravLevel 1.1.0 loaded`.

## Configuration

The config file is created after the first launch with the mod:
`BepInEx\config\com.jammytoast.starshipevo.gravlevel.cfg`

```ini
[Controls]
LevelKey = B
FaceKey = N
AlwaysLevelWhileHolding = false

[Leveling]
YawSnap = true
YawSnapDegrees = 90
FaceDirection = Forward
UseShipFront = true
Strength = 25
Damping = 10
MaxAngularAccel = 20

[Debug]
VerboseLogging = false
```

| Key | What it does |
|---|---|
| `LevelKey` | The key to hold while grabbing. |
| `FaceKey` | The key to hold to level the object and turn its front toward you. Yaw snap is skipped while it is held. |
| `AlwaysLevelWhileHolding` | Level everything you grab, without holding a key. |
| `YawSnap` / `YawSnapDegrees` | Snap the heading to the nearest step (it turns at most half a step). |
| `FaceDirection` | Which side is the front for `FaceKey`: `Forward`, `Backward`, `Left` or `Right`. |
| `UseShipFront` | For crafts with a yoke, use the pilot's forward as the front. Turn off to use the build grid's forward. |
| `Strength` | How hard it pulls toward level. Raise it if leveling is too slow. |
| `Damping` | How quickly it settles. Raise it if it wobbles or overshoots. |
| `MaxAngularAccel` | The cap on turning speed (rad/s²). |
| `VerboseLogging` | Writes debug info to the log once per second. |

Restart the game after you change the file.

## Uninstall

**To remove only GravLevel:** delete the `BepInEx\plugins\GravLevel` folder. You can also delete `BepInEx\config\com.jammytoast.starshipevo.gravlevel.cfg`.

**To remove BepInEx completely:** in the game folder, delete
- the `BepInEx` folder
- `winhttp.dll`
- `doorstop_config.ini`
- `.doorstop_version` and `changelog.txt`, if they exist

If other mods (like [OreFix](https://github.com/JamMyTo4st/StarshipEVO-OreFix)) also use BepInEx, removing it disables them too. You can then run Steam > right-click Starship EVO > **Properties** > **Installed Files** > **Verify integrity of game files**.

## Notes

- In multiplayer, the leveling probably only applies where your game has physics control of the object, the same as vanilla grabbing.
- Objects in anti-grav stasis may respond differently.
- The game is in Early Access and changes often, so an update can break this mod. If something breaks, remove the mod (see above).

## Building from source

The game's DLLs are **not** in this repo. You need your own copy of the game.

1. Install the [.NET SDK](https://dotnet.microsoft.com/download) (6 or newer).
2. Build:
   ```
   dotnet build -c Release
   ```
   If the game is not in the default Steam folder (`C:\Program Files (x86)\Steam\steamapps\common\Starship EVO`), pass the path:
   ```
   dotnet build -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Starship EVO"
   ```
3. The output is `bin\Release\netstandard2.0\GravLevel.dll`. Copy it to `BepInEx\plugins\GravLevel\`.

## Reporting issues

Open an issue here and attach your `BepInEx\LogOutput.log`.

## License

[MIT](LICENSE) © 2026 JamMyTo4st
