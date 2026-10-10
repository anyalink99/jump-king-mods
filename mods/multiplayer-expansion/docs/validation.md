# Development and validation

Run commands below from the repository root. Building needs the installed
game and original Multiplayer; Steam probes also need a signed-in client.

## Build and package

```powershell
.\scripts\check-mods.ps1 -Mod multiplayer-expansion
.\mods\multiplayer-expansion\start-lab.ps1 -Probe
```

Focused checks cover shared assembly loading, keyboard and UIAPI+ pointer focus,
local packet framing and ordering, predicted contacts, session transitions, cancellation
and staging isolation. The Steam probe
starts two processes and compares 20 packets of 4096 bytes in each direction.
It needs the signed-in Steam client; receipts stay in the session folder.

The build produces the native mod and its background client helper under
`build/multiplayer-expansion/LAB/`, and a checked release under
`build/multiplayer-expansion/UPLOAD_TO_WORKSHOP/`. The release includes the SDK
shell with its embedded implementation and world callbacks, a thin client
bootstrap, Debug staging helpers, licenses and a preview smaller than 35,000 bytes.
It doesn't redistribute Jump King, JK Runtime or Multiplayer.
It's an explicit check target because it needs the third-party mod installed.

See [changes](../CHANGELOG.md) and [third-party notices](../THIRD_PARTY_NOTICES.md).

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
switching. It uses the same temporary Subframe Charge suspension. Use `-MultiplayerOnly` to keep Multiplayer,
this expansion and the required JK Runtime dependency and isolate a mod
conflict, `-ModDirectory` to add extra folders with their dependencies, or
`-StageOnly` to prepare files without launching. A compiled map must contain
`level_settings.xml`; compilation doesn't verify its route.

## Reproduction

The Debug network controls inject faults after the underlying transport handshake.
Both endpoints read the session's `network-test.txt`. Its fields are one-way delay
(0–500 ms), jitter (0–300 ms), loss (0–50%), duplication (0–25%), positive seed, and
recording token. The menu writes this file atomically. New sessions start clean.

`PacketSchedule` uses a deterministic random sequence. Varying due times can reorder
packets even when the underlying transport is reliable. Changing conditions keeps
already scheduled due times; Reset changes the seed and clears pending delivery.

`client1.network.trace` and `client2.network.trace` contain relative timestamps,
sent/received direction and encoded movement packets. Recording excludes legacy
and shared-world payloads. Verification replays packet acceptance and prediction twice and compares
state hashes. It is a network-state reproduction, not an input or full-game replay.

Run `scripts/check-mods.ps1 -Mod multiplayer-expansion -NoCache` from the repository
root. The focused suite checks protocol boundaries, state/event lifetimes, seeded
four-peer worlds, local transport, native hooks, UI input and bounded recording.

## What to check in the game

Test Ghosts and Solid, pushing on/off, head stacking, carrier jumps, airborne
bounces and a menu opened while carrying. Try a kick, a blocked kick and teleport
near terrain. Repeat across a side link with Smooth Camera enabled.

For shared worlds, trigger a bird, hidden wall and supported switch from each
player, then pause the host and leave synchronization. Check that personal
inventory and saves remain local. Test latency as well as a clean local link.

Focused tests cover protocol boundaries, epochs, seeded four-player worlds,
actions, local framing, native hooks, UI focus and recording limits. World tests
cover installed lever behavior, the eight Switch Blocks data layouts and save
isolation. These fixtures don't establish every map or real internet route.
