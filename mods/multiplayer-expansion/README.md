# Multiplayer Expansion

Multiplayer Expansion 0.1.0 adds a two-client test mode to Jump King's Debug menus.
The running game becomes client 1, and client 2 appears inside the same window.
Both games keep independent simulation and use the installed Multiplayer mod's
packet format and ghost players. Both render at up to **60 FPS**.

## Install and start

Keep Steam running and install the
[Multiplayer mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3190590114).
Build and install Multiplayer Expansion from this repository:

```powershell
.\scripts\check-mods.ps1 -Mod multiplayer-expansion
.\mods\multiplayer-expansion\install.ps1
```

Close the game before installing. Launch the main game in native Debug mode:

```powershell
.\mods\multiplayer-expansion\start-debug.ps1
```

Client 2 starts automatically with a **Local** connection.
Open **Multiplayer Expansion** in the Debug game's mod settings to control it,
from the main or pause menu. These controls are hidden and inactive in normal gameplay:

- **Two clients: Steam** uses Steam Networking Sockets over loopback, as in the
  original standalone lab.
- **Two clients: Local** sends the same packets through a Windows named pipe.
  It doesn't use Steam networking. The game still needs its normal Steam session.
- **Switch input client** changes which game receives your existing bindings.
  The two buttons above the game views do the same thing.
- **Stop / cancel client 2** cancels preparation or closes the extra game and
  restores the original window. The embedded window also has a **Stop client 2** button.

Selecting the other connection mode stops the current client and prepares a fresh
one. Repeated starts in the same mode don't create duplicate clients. Cancel also
discards any queued restart. A failed start stays stopped until you retry.

The second client is prepared in the background, at lower process priority.
The menu and window title report the preparation stage. Input selection stays
disabled for client 2 until it has drawn its first frame and connected.
Once ready, it connects
without a lobby or invitation. The footer shows the selected client, connection
state and packet counts. Esc and the other menu bindings work with Subframe
Charge's keyboard sampler too. UIAPI+ mouse coordinates and focus follow the
selected embedded client. Its usual first-click cursor activation still applies.
The integrated mode doesn't reserve a new hotkey.
The 60 FPS draw cap also applies while preparing the second client. If preparation
fails, the menu shows a short error; details are in the new session's `native.log`.
Closing the main game cancels preparation and shuts down its helper. Preparation
times out after five minutes, client startup after two minutes, and a stuck stop
gets eight seconds before the owned helper is terminated. Partial files and logs
are retained for diagnosis; cancelled preparations aren't launched.

Native startup, performance, assembly-load and hook logs keep three generations
of at most 4 MiB per file. Before preparing a new client in the standard `sessions`
folder, staging keeps the two newest full client copies and trims reproducible
resources and binaries from older sessions. Running clients and sessions less than
an hour old are spared. Settings, saves, replays and diagnostics stay in place;
the base-game Debug cache is separate and isn't trimmed by this rule.

The mode requires the game's native `-debug` launch. Stopping client 2 leaves
client 1 in that same Debug session.
Client 1 remains your running game and uses its existing saves and settings.
Client 2 gets separate writable files, saves, settings and copies of installed
local/Workshop mods. Steam achievement/stat writes are blocked while testing.

When a custom map is open, client 2 gets a copy of that compiled map. Use the same
map in both clients when testing ghosts. This mod doesn't copy live player state
or synchronize map selection. It preserves Multiplayer's existing gameplay scope.
The initial Debug map uses one session identity in both copies; an old Workshop ID
in a saved attempt won't hide the other player. Other maps keep their normal IDs.

The installer puts session data in `build/_work/multiplayer-expansion/sessions/`.
Its installed `session-root.txt` can choose another folder. Without that file,
the mod uses `%LOCALAPPDATA%/JumpKing/MultiplayerExpansion`. Sessions and rollback
backups are retained. Installed game and third-party mod binaries aren't overwritten.

## Developer launch options

`start-debug.ps1` launches the installed `JumpKing.exe -debug <map>` directly,
with the Steam environment set. With no `-MapDir`, it prepares and reuses a copy
of the base game's content. Use `-RefreshBase` after changing installed base assets,
or `-MapDir` for another compiled map. There is no separate launcher window.
The default is `-Connection Local`. Use `-Connection Steam` for Steam loopback,
or `-Connection Off` to start one game and enable client 2 later from its menu.

The standalone lab remains available for completely isolated tests:

```powershell
.\mods\multiplayer-expansion\start-lab.ps1
.\mods\multiplayer-expansion\start-lab.ps1 -MapDir 'C:\path\to\compiled-map'
```

It creates two copied clients in its own window, with input buttons and F6 for
switching. Both render at up to 60 FPS. Use `-MultiplayerOnly` to isolate a mod
conflict, `-ModDirectory` to add extra folders with their dependencies, or
`-StageOnly` to prepare files without launching. A compiled map must contain
`level_settings.xml`; compilation doesn't verify its route.

## What the connections test

Both transports keep Multiplayer's `TrackData` serialization, parser, ghost
tracking and drawing. State crosses a real process boundary. The local mode uses
framed, ordered messages through a named pipe; Steam mode adapts the legacy P2P
packet boundary to `CreateListenSocketIP` / `ConnectByIPAddress` with reliable
ordered delivery. Both require the session's random handshake token.

Steam mode binds only `127.0.0.1`. Two local peer IDs identify the ghosts. Steam
lobbies and invitations are disabled during the test. This checks packet handling
and ghost synchronization, not distinct-account authentication, NAT traversal,
internet latency or Steam Datagram Relay. Those require a separate online test.
The original Multiplayer mod synchronizes player ghosts, not arbitrary map state
or interactions.

The installed Multiplayer binary reviewed on 2026-10-07 has SHA-256
`4679E787A31504FD598E691C078866A612E861C2073463E90C7174ADC88670D0`.
The adapter depends on its methods; updates need validation. Sessions record
input binary hashes, draw/update rates and memory use for diagnosis.

## Checks and package

```powershell
.\scripts\check-mods.ps1 -Mod multiplayer-expansion
.\mods\multiplayer-expansion\start-lab.ps1 -Probe
```

Focused checks cover shared assembly loading, keyboard and UIAPI+ pointer focus,
local packet framing and ordering, draw caps, session transitions, cancellation
and staging isolation. The Steam probe
starts two processes and compares 20 packets of 4096 bytes in each direction.
It needs the signed-in Steam client; receipts stay in the session folder.

The build produces the native mod and its background client helper under
`build/multiplayer-expansion/LAB/`. This is a local development package, not a
Workshop upload package, and doesn't redistribute Jump King or Multiplayer.
It's an explicit check target because it needs the third-party mod installed.

See [changes](CHANGELOG.md) and [third-party notices](THIRD_PARTY_NOTICES.md).
