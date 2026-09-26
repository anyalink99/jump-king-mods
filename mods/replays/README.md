# Replays

[Technical documentation](docs/index.md).

Record Jump King runs, watch them with seek controls, or race a translucent
ghost. Replays use recorded positions, so custom movement does not need to be
resimulated.

Version **2.2.0** requires [JK Runtime 1.30 or newer 1.x](../jk-runtime/README.md).

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
the live run or change persistent statistics.

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

## Build

From the repository root:

```powershell
.\mods\replays\build.ps1
```

The runtime SDK packages the embedded implementation into
`build/replays/UPLOAD_TO_WORKSHOP/Replays.dll`. Install it with JK Runtime.

## Optional completion verification

[Run Verifier](../run-verifier/README.md) can associate saved replays with completion
reports. It is optional; Replays still records one session at a time.

For integration, `Replays.VerificationBridge` API 1 exposes `IsPlayback`,
`BindRun(runId + ":" + sessionId)`, `Watch(id)` and
`Saved(binding, replayId, path, startSeconds, endSeconds)`. Bind on the game thread
after recording starts. `Saved` runs on the writer thread after an atomic save;
handlers must not access game objects. Manual snapshots retain their capture
association. Restarted recordings need a new binding. Positional recordings are
not a deterministic simulation or a cheat verdict.
