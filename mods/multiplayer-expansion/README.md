# Multiplayer Expansion

Play Jump King with solid players, head stacking, bounces and kicks, or keep
players as ghosts. Optional world synchronization lets the host run supported
world mechanics for everyone. A Debug mode puts two connected games in one
window for testing.

## Install and connect

Multiplayer Expansion **0.8.0** needs **JK Runtime 2.0+** and the original
Multiplayer mod. Install the same expansion version and map mods on each player.

1. Subscribe to [Multiplayer](https://steamcommunity.com/sharedfiles/filedetails/?id=3190590114),
   [JK Runtime](https://steamcommunity.com/sharedfiles/filedetails/?id=3793086563)
   and [Multiplayer Expansion](https://steamcommunity.com/sharedfiles/filedetails/?id=3816170337).
2. Keep Steam running. Wait for the downloads, then restart Jump King.
3. Use the original **Multiplayer** menu to create or join a lobby. Load the same
   map as the other players.
4. Open **Mods > Multiplayer Expansion > Multiplayer settings**. The host
   chooses the shared rules under **Host Settings**; each player edits Kick in **Binds**.

Other players can remain visible without this expansion, but they won't take
part in its physical interactions. See [compatibility](docs/networking.md#version-compatibility).

## Choose the rules

| Host setting | What it does | Initial setting |
| --- | --- | --- |
| Mode: Ghosts / Solid | Ghosts pass through each other. Solid players can stand on heads, collide and bounce. | Ghosts |
| Push other players | Walking into a grounded player can move them. Only available in Solid. | On when first using Solid |
| World synchronization | The host runs supported world mechanics; any player can trigger their interactions. | Off |
| Allow kicks | Players can hit each other with a short forward shockwave. | On |
| Allow teleport | Players can teleport near another player on the same map. | On |

Guests follow the host's rules without losing their own saved host defaults.
Switching between Ghosts and Solid remembers the pushing preference.

In Solid, a king can jump while carrying another king, and the passenger can
jump from that king while both are airborne. A grounded king doesn't move just
because someone lands on him. Opening a menu freezes your living king in place;
other players can still stand on him. Read [player interactions](docs/player-interactions.md)
for carrying, bounces, equipment and pause behavior.

## Appearance, kick and teleport

Native hats, boots, clothing, capes and rings follow each player's equipment.
Your installed map and skin supply the artwork. Wardrobe+ custom textures and
animation packs aren't transferred.

**Multiplayer > Display Options > Ghost opacity** remembers a separate value
for Ghosts and Solid. Ghosts starts with your existing opacity, normally 60%;
Solid starts at 100%. You can change either from 0% to 100%.

**Kick** defaults to **K** on keyboard; controllers start unbound. Open **Binds**
to change it. Terrain blocks the shockwave. **Teleport to <name>** buttons appear
directly below Multiplayer settings, one per peer. Teleport looks for free space
near the target and leaves you where you are if it can't find any.
See [player actions](docs/player-actions.md) for range, permissions and placement.

## Share the world

Turn on **Host Settings > World synchronization** to share wind, native ravens,
hidden walls and the supported Switch Blocks state. MME 0.8.0 also shares its
declared scene state. This works with Ghosts and Solid; inventory stays personal.

Everyone needs matching maps and world mods. An incompatible world layout is
reported in the setting's description. Unknown mod mechanics aren't automatically
synchronized. Read [world synchronization](docs/world-synchronization.md) for
supported mechanics, pause behavior and what happens when the session ends.

## Test two clients in Debug

Launch the game with `-debug "<compiled map directory>"`. Open **Multiplayer
settings > Two-client test tools > Start two clients: Local**. The second game
appears beside the first in the same window; no checkout or separate launcher
is needed.

Press `6` on the number row, or use the buttons above the views, to switch full
input. The inactive client also accepts `[` for left, `]` for right and
`\` for jump. **Stop / cancel client 2** returns to one game.

Local uses a named pipe; Steam uses local Steam sockets. Both exchange real
packets between separate game processes. The test doesn't establish real
internet or relay behavior. See [two-client Debug testing](docs/debug-testing.md)
for startup, input, network faults, logs and storage.

## Limits and troubleshooting

Latency and lost packets can still produce corrections. Solid interactions mark
the run as modified. World synchronization doesn't share saves or inventory,
and unknown foreign mechanics need a supported integration.

For a missing player, failed connection or startup error, use the
[troubleshooting steps](docs/debug-testing.md#troubleshooting). The
[documentation index](docs/index.md) links the technical guides.

## Build from source

With Jump King and the original Multiplayer mod installed, run from the repository root:

```powershell
.\scripts\check-mods.ps1 -Mod multiplayer-expansion
.\mods\multiplayer-expansion\install.ps1
```

Close the game before installation. The build creates
`build/multiplayer-expansion/UPLOAD_TO_WORKSHOP/`. Read
[development and validation](docs/validation.md) for launch helpers and checks.
Release history is in [CHANGELOG](CHANGELOG.md); licenses are in
[third-party notices](THIRD_PARTY_NOTICES.md).
