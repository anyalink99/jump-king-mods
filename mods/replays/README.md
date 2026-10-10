# Replays

Record runs, watch them with seek controls or race a translucent ghost. Recorded
positions preserve custom movement without resimulating it.

## Install

Version **2.7.0** needs [JK Runtime 2.0+](../jk-runtime/README.md) and one loaded
Harmony 2 engine, such as the copy supplied by Subframe Charge or Mapping.
Wardrobe+ 2.2+ is optional and adds recorded cosmetic changes.
For a manual install, close Jump King and put the release `Replays.dll` in
`Jump King/Content/JKMods/`. Install Runtime separately and keep one active copy.

## Saving and watching

Open `Replays` from the main menu or pause menu to browse recordings by world,
date and duration. Select a recording to watch it, use it as a ghost, or delete it.

A run stays in memory until you complete the game or choose `Save replay` in
Pause. Ordinary exits and restarts do not save unfinished runs. Manual saving
takes a snapshot without stopping the recording; saving again creates another
entry.

The viewer supports play/pause, one- and ten-second seeking, and a HUD toggle
on the Boots binding. Watching from the main menu starts the currently loaded
world. Closing playback returns to the main menu. Playback does not complete
the live run or change persistent statistics. Clip duration uses the native IGT
step (normally 17 ms), rather than assuming 60 ticks per second. Completed new
recordings also show the exact completion IGT separately from clip length.

The library and viewer show the current device's bound buttons. The viewer
separates seeking from playback commands, with Play/Pause following its state.
Hints update after rebinding or changing controllers and use the shared UIApi+ theme.
Left-click once to reveal the mouse cursor, then click recordings and commands.
Scroll in the library to browse; in the viewer, scroll to seek by one second
per step or click the progress bar to jump. Right-click closes playback. Click
the scene to restore a hidden HUD.

A recording must match the current world and its gameplay-asset hash. Runtime
prepares a fresh hash before each attempt, preventing playback on a different
revision of the same map.

## Settings and files

- `Replays in main menu` shows the root-menu library entry.
- `Record new runs` controls automatic recording.
- `Save replay in pause menu` shows the manual-save shortcut. When hidden,
  saving remains available in the Replays mod section.

After saving, the selected row displays `Saved!` for one second.

Recordings are compressed `.jkr` files in
`Jump King/Content/Replays/Files`. Settings live in the parent `Replays`
directory. See [recording and playback](docs/recording.md) for format,
equipment and timing details.

## What a recording preserves

The current format 6 stores poses, native equipment, sound events and declared
Runtime world state. Viewing restores that world without advancing gameplay
logic or writing personal saves. Race ghosts stay silent and leave the live
world independent. Unknown foreign mechanics aren't captured automatically.

Formats 2–5 remain readable. Older files can't recover equipment, surface sounds
or exact completion times that weren't recorded. Seeking rebuilds supported
cosmetic effects silently; playback uses your loaded assets and volume settings.
Stop world synchronization before viewing a world recording in a connected lobby.
Read [recording and playback](docs/recording.md) for clocks, bounds and ownership.

## Optional completion verification

[Run Verifier](../run-verifier/README.md) can associate saved recordings with
completion reports. Positional recordings aren't a deterministic simulation or
a cheat verdict. Mod authors can use the [completion bridge](docs/integration.md).

## Build

From the repository root:

```powershell
.\mods\replays\build.ps1
```

The runtime SDK packages the embedded implementation into
`build/replays/UPLOAD_TO_WORKSHOP/Replays.dll`. Install it with JK Runtime.

The [guide index](docs/index.md) links the format and integration references.
