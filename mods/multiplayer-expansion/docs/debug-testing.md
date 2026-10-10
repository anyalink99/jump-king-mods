# Test two clients in one window

Use this mode to test player interactions or a compiled map without a second
computer. The running game is client 1; a background game becomes client 2 in
the same window. Both have independent simulation and exchange actual packets.

## Start from the installed Workshop mod

1. Install Expansion 0.8.0, Runtime 2.0+ and original Multiplayer. Keep Steam running.
2. Launch Jump King with `-debug "<compiled map directory>"`.
3. Open **Mods > Multiplayer Expansion > Multiplayer settings > Two-client test tools**.
4. Choose **Start two clients: Local** and wait for client 2 to connect and draw.

No checkout or separate launcher is needed. Client 1 is the host. The menu and
window title show preparation progress; the footer shows connection state and
packet counts. Client 2 input stays disabled until its first frame and connection.

## Control either game

| Control | Action |
| --- | --- |
| Number-row `6`, or the buttons above the views | Switch which game receives your normal bindings and menu input |
| `[` | Walk left in the inactive game |
| `]` | Walk right in the inactive game |
| `\` | Charge and release a jump in the inactive game |
| **Stop / cancel client 2** | Cancel preparation or close client 2 and return to one view |

The bracket and backslash controls are physical keys, including on a Russian
layout. They only move and jump; they don't operate menus or items. They stop
while their target is in a menu or input capture. Mouse controls follow the
selected view; the usual first click reveals the cursor.

Input stops outside the shared window and during Steam Overlay. Release held
keys after switching or returning to the game. Holding 6 switches once. These
reserved keys don't replace saved bindings.

## Local or Steam

Local sends packets through a Windows named pipe. Steam uses Steam Networking
Sockets over loopback. Selecting another mode stops the current second client
and prepares a fresh one. Repeated starts in the same mode don't add clients.
Cancel discards queued restarts; a failed start waits for you to retry.

Both modes use the same packet formats. Steam loopback doesn't test internet
latency, NAT or relay routing; see [transport limits](networking.md#local-and-steam-tests).
The game still needs its normal Steam session in Local mode.

During preparation and two-client play, Subframe Charge's **Subframe Inputs**
and **240 Hz** are temporarily inactive and greyed out. If installed, it must be
0.27.2+. Saved preferences return when client 2 stops, including after a failed
start. Charge correction and Quarter-step Charge stay unchanged. Expansion
doesn't add an FPS limiter.

## Test a bad connection

Open **Two-client test tools > Network test conditions**. Set one-way delay,
jitter, loss or duplicate packets; jitter may reorder delivery. Both clients
use the same conditions, after the transport handshake. Ordinary online lobbies
aren't affected. Changes apply within 250 ms. Reset clears queued test packets
and restores a clean link.

Record a short interaction with **Record network**, stop it, then choose
**Verify last packet recording**. Verification replays the selected client's
movement packets twice in the background and compares state hashes. It doesn't
replay keyboard input, map scripts or shared-world packets.

Recordings stop at two minutes or 4 MiB per client. Each client keeps its current
trace and two backups. Read [packet reproduction](validation.md#reproduction)
for the file fields and deterministic delivery schedule.

## Maps, settings and storage

Client 1 keeps the current Debug map, saves and settings. Client 2 receives a
copy of that compiled map and separate writable saves, settings and installed
mods. Live player state and later map selections aren't copied. The initial
map gets one session identity in both clients; other maps retain normal IDs.
Steam achievement/stat writes are blocked during the test.

Workshop installs default to `%LOCALAPPDATA%/JumpKing/MultiplayerExpansion` for
sessions unless `session-root.txt` beside the mod chooses another location.
The repository installer sets `build/_work/multiplayer-expansion/sessions/`.
Installed game and third-party mod binaries aren't overwritten by preparation.

Logs keep three generations of at most 4 MiB per file. Staging retains the two
newest complete client copies and removes reproducible binaries/resources from
older sessions. Running clients and sessions younger than an hour are spared.
Saves, settings, replays, logs and rollback backups are retained. The separate
base-game Debug cache isn't trimmed by that rule.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| No Two-client test tools | The game must have been launched with its native `-debug` argument. |
| Client 2 won't start | Read the menu error and the session's `native.log`. Fix the cause, then retry. |
| Connected but no other king | Check both clients use the same map, have finished spawning and have original Multiplayer enabled. Check packet counts in the footer. |
| No collisions | The host must select Solid. Both peers need compatible Expansion packets and an acknowledged rule set. |
| World doesn't follow the host | Enable World synchronization and read its status for map or schema mismatch. |
| An input stays blocked after switching | Release the key/button, focus the shared window and try again. Use 6 for full menu control of the other game. |

Preparation times out after five minutes; client startup after two. A stuck stop
gets eight seconds before its owned helper is terminated. Partial files and logs
stay for diagnosis. Closing the main game cancels preparation and shuts down its
helper. Stopping client 2 leaves client 1 in the same Debug session.

## Input adapter details

The Debug input adapter keeps native keyboard/controller identity through Runtime
pad layers and routes both existing and new mouse layers. `FocusButtons` requires
release after focus or client changes before forwarding native button arrays.
The shared physical sampler and UI pointer use the same selected-client and Steam
Overlay gates. Stopping the test restores native-host readers and focus delegates
in reverse order; attempt input actions and contact phases belong to Runtime scopes.

For repository launch helpers and isolated Steam probes, read
[development and validation](validation.md).
