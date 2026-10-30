# Aion Meter

A DPS meter for **Aion 4.8** (built for Gamez Aion, works with any English 4.x client).
It reads the game's `Chat.log`, the same way Aion RainMeter does, and never touches game files or network traffic.

## Features

- Always-on-top overlay that ranks everyone by damage: highest on top, then second, third…
- Shows allies and enemy players near you, plus monsters if you turn them on
- Class icon and colour for each player (Gladiator, Templar, Assassin, Ranger, Sorcerer, Spiritmaster, Cleric, Chanter, Gunner, Songweaver, Aethertech)
- Click the overlay to open the full list: damage, DPS, %, hits, crit rate and max hit, with a per-skill breakdown for whoever you select
- Keeps adding up until you press Reset (idle time doesn't lower your DPS)
- Copy everyone's damage to the clipboard in one click, ready to paste in Discord or chat
- Healing meter: switch between damage and healing with one click (heals over time credited to the caster)
- DoT ticks credited to whoever applied them; your spirits/servants can count as your damage
- Damage taken, pause and reset
- Filter by class, monsters and bosses
- Bosses are detected automatically and shown in red with **BOSS** in front of the name
- Shrink the whole meter into a tiny cube and click it to bring it back

## Install

1. Open the [latest successful build](https://github.com/Anis1988/aionmeter/actions/workflows/build.yml?query=branch%3Amain+is%3Asuccess), scroll to **Artifacts**, download **AionMeter-win-x64** (you must be signed in to GitHub) and unzip it anywhere.
2. Run `AionMeter.exe`. If Windows SmartScreen shows a warning, click **More info → Run anyway**.
3. Click the ⚙ button and set:
   - **Game folder**: the folder that contains `Chat.log` and `system.cfg` (default `C:\GamezAION 4.8\GamezAION 4.8`)
   - **Your character name** and **Your class**

The chat log must be on: `g_chatlog = "1"` in `system.cfg` (AionSysEditor can set it).
Close Aion RainMeter while using this, because its "Clear Chat.log on start-up" option will wipe the log.

## Using it

| Button | Action |
|---|---|
| DMG / HEAL | Switch between ranking by damage and by healing |
| ⏸ | Pause / resume counting |
| ⧉ | Copy the top players' damage to the clipboard (top 10 by default, change it in settings; 0 = everyone) |
| ⟳ | Reset: clear everything and start over |
| ☰ | Full list (same as clicking the overlay) |
| ⚙ | Settings |
| Filter | Show or hide each class, monsters and bosses |
| ⌃ | Collapse to just the title bar (click again to expand) |
| Cube | Shrink everything into a tiny cube; click the cube to restore it, drag it to move it |

- Drag the title bar to move the overlay and the bottom-right corner to resize it.
- Damage never resets on its own; press ⟳ to start over. Breaks longer than 15 seconds don't count toward DPS time.
- The full list also has a **Copy** button that uses the same setting.
- In the full list, **right-click a player** to set their class if it wasn't detected. Your choice is remembered.

## How classes are detected

The meter guesses each player's class from the skills they use. It knows a list of common skills
and also learns new ones: every skill used by you (with your class set) or by a player whose class
you set by hand is saved, so detection improves the more you play.

Players who only auto-attack show a grey `?` icon until they use a known skill.

## Bosses

A monster that takes more than 1,500,000 damage in one life (between kills) is treated as a boss:
its bar turns red and its name gets **BOSS** in front. Change the amount in settings, or right-click
a monster in the full list and choose **Mark as boss** (remembered between sessions).

## Custom icons

The meter draws its own vector class emblems. To use other icons (for example the in-game ones),
put PNG files named after the class (`Spiritmaster.png`, `Sorcerer.png`, …, plus `Monster.png` and
`Boss.png`) in the `Icons` folder next to `AionMeter.exe`.

## Limitations

- Healing is read from lines like `Grop recovered 2,345 HP because Lumi used Healing Light.` If your server words heals differently, they won't show up yet.
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

Every push to `main` builds the app in GitHub Actions; the exe is attached to the run as the `AionMeter-win-x64` artifact. Pushing a `v*` tag also publishes a release with the zip.

Settings are stored in `%AppData%\AionMeter\settings.json`.
