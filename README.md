# Named Saves Mod

A Sailwind BepInEx 5 plugin. Name the saves in the game's six slots (0-5), and keep extra saves in a Save Menu that you can swap into any slot.

## Use

On the title screen, choose New Game or Continue to reach the slot menu. A **Save Menu** button appears at the top right.

- **Name a slot**: type in the box beside it. The name shows on the slot button above the time and date.
- **Store**: moves a slot's save to the stored list and empties the slot.
- **Swap with slot N / Move to slot N**: pick a slot on the left, then press the button on a stored save. A save already in that slot takes its place in the stored list.
- **Slot Information Panel**: select a slot to see when it was last played, write a description, and set a badge.
- **Badge**: Set badge opens a picture (PNG or JPG), lets you drag a square over it and size it, and saves that square. The badge shows in the Save Menu and in the top left corner of the slot button.

The name, description and badge travel with the save. No save file is ever deleted or overwritten.

With NANDTweaks installed, its save thumbnails follow the save too, and the slot buttons reload them after a swap.

## What moves

A slot is more than `slotN.save`. The mod moves everything named `slotN` followed by `.` or `_` (the save, its five backups, and files other mods keep beside it such as `slotN.save.png`), plus a `slotN` folder if there is one.

Stored saves live in `%USERPROFILE%\AppData\LocalLow\Raw Lion Workshop\Sailwind\NamedSaves\library\<timestamp>\`, with `slotN` renamed to `slot`, plus `name.txt`, `description.txt` and `badge.png`. For saves in a slot, the names are in `NamedSaves\slot-names.txt` and the rest in `NamedSaves\slotN-description.txt` and `NamedSaves\slotN-badge.png`.

## Config

`BepInEx\config\com.dizzy.sailwind.namedsaves.cfg`

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | true | Show the Save Menu button |
| `ShowNamesOnSlotButtons` | true | Put the name on the slot buttons |
| `SlotButtonNameLength` | 16 | Longest name shown on a slot button |
| `ShowBadgesOnSlotButtons` | true | Put the badge on the slot buttons |
| `SlotButtonBadgeSize` | 0.4 | Badge size as a fraction of the button's shorter side |

## Build

```
dotnet build src/Dizzy.NamedSaves/Dizzy.NamedSaves.csproj -c Release
```

Override the game path with `-p:SailwindDir=...`.
