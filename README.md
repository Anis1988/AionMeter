# Aion Meter

A DPS meter for **Aion 4.8** (built for Gamez Aion, works with any English 4.x client).
It reads the game's `Chat.log`, the same way Aion RainMeter does, and never touches game files or network traffic.

## Features

- Always-on-top overlay that ranks everyone by damage: highest on top, then second, third…
- Shows allies and enemy players near you, plus monsters if you turn them on
- Class icon and colour for each player (Gladiator, Templar, Assassin, Ranger, Sorcerer, Spiritmaster, Cleric, Chanter, Gunner, Songweaver, Aethertech)
- Click the overlay to open the full list: damage, DPS, %, hits, crit rate and max hit, with a per-skill breakdown for whoever you select
- Fight history: every fight is kept, plus a whole-session total
- DoT ticks credited to whoever applied them; your spirits/servants can count as your damage
- Damage taken, pause and reset

## Install

1. Download `AionMeter-win-x64.zip` from the [latest release](https://github.com/Anis1988/aionmeter/releases/latest) and unzip it anywhere.
2. Run `AionMeter.exe`. If Windows SmartScreen shows a warning, click **More info → Run anyway**.
3. Click the ⚙ button and set:
   - **Game folder**: the folder that contains `Chat.log` and `system.cfg` (default `C:\GamezAION 4.8\GamezAION 4.8`)
   - **Your character name** and **Your class**

The chat log must be on: `g_chatlog = "1"` in `system.cfg` (AionSysEditor can set it).
Close Aion RainMeter while using this, because its "Clear Chat.log on start-up" option will wipe the log.

## Using it

| Button | Action |
|---|---|
| ⏸ | Pause / resume counting |
| ⟳ | Reset all fights |
| ☰ | Full list (same as clicking the overlay) |
| ⚙ | Settings |

- Drag the title bar to move the overlay and the bottom-right corner to resize it.
- A new fight starts after 20 seconds without damage (changeable in settings).
- In the full list, **right-click a player** to set their class if it wasn't detected. Your choice is remembered.

## How classes are detected

The meter guesses each player's class from the skills they use. It knows a list of common skills
and also learns new ones: every skill used by you (with your class set) or by a player whose class
you set by hand is saved, so detection improves the more you play.

Players who only auto-attack show a grey `?` icon until they use a known skill.

## Custom icons

The overlay draws its own coloured class badges. To use other icons, put PNG files named
after the class (`Spiritmaster.png`, `Sorcerer.png`, …) in the `Icons` folder next to `AionMeter.exe`.

## Limitations

- Only what's in your `Chat.log` can be counted. Players far away, or whose damage your client doesn't log, won't appear.
- Monsters and players are told apart by name: Aion character names are a single word, monster names usually aren't.
- English client only.

## Building

Requires the .NET 8 SDK.

```bash
dotnet test tests/AionMeter.Core.Tests
```

```bash
dotnet publish src/AionMeter/AionMeter.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Every push to `main` builds the app in GitHub Actions. Pushing a `v*` tag publishes a release with the zip.

Settings are stored in `%AppData%\AionMeter\settings.json`.
