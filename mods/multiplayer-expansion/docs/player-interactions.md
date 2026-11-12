# Player interactions and appearance

Connect through the original Multiplayer menu, then open **Multiplayer Expansion >
Multiplayer settings > Host Settings**. The host selects the mode. Everyone needs
the expansion for physical interactions; a player without it remains a ghost.

## Ghosts and Solid

Ghosts is the initial mode. Players pass through each other. Solid enables
standing on heads, side and underside collisions, and airborne bounces.
Standing and body collisions are part of Solid and have no separate switches.

A king in the air rebounds from a grounded king with the native wall-bounce
coefficient. Two airborne kings share the relative collision impulse. A carrier
can jump with a passenger. The passenger can jump again from that airborne king,
inheriting its movement at takeoff. Snake Ring applies ordinary ice friction on
a head.

**Push other players** controls walking pushes in Solid. With it off, the walker
stops at a grounded king; landing and bouncing don't shove that grounded king.
Head carrying, airborne bounces and recovery from an overlapping spawn still work.
The preference is remembered when switching to Ghosts and back.

## Menus and overlaps

Opening a native pause menu or Runtime page keeps a living king visible and
solid at the same position, including in midair. Other players may stand on him.
Resuming discards impacts from before the pause. A dead or replaced body doesn't
become solid because its menu is open.

Spawn, teleport and persistent overlaps try to find nearby free space without
crossing terrain or placing kings inside each other. The search is limited to
96 pixels and 256 candidates. If no safe placement exists, normal map movement
remains available and recovery retries. Recovery doesn't add bounce impulses.

Bounces use the native bump sound, including its underwater variant. A native
terrain impact and player impact in the same frame play one bump. Walking pushes
and overlap recovery stay silent.

## Equipment and opacity

Native equipment changes travel with movement snapshots. They don't change
another player's inventory, unlocks or worn items. Artwork comes from each
receiver's map and skin. Wardrobe+ custom textures, materials and animation packs
aren't transferred, so those appearances may differ.

Use **Multiplayer > Display Options > Ghost opacity**. There is one control with
two saved values: Ghosts inherits the old Multiplayer opacity, normally 60%, and
Solid starts at 100%. Both accept 0%–100%. The host chooses the mode; each player
chooses their own opacity.

## Saved rules and compatibility

`interactions.txt` stores your host mode and pushing default;
`opacity-profiles.txt` stores the two local opacity values. Both live beside the
expansion. Guests follow session rules without overwriting saved host defaults.
Old Platforms and partial Custom modes migrate to Solid, retaining pushing.

Solid needs an acknowledged rule set, matching map identity and a movement
sample less than half a second old. Enabling physical interactions registers a
native external body behavior, so the run isn't unmodified for achievements.

Latency beyond the prediction window and long losses can cause corrections.
Read [contact ownership](contacts.md) for the update sequence and
[networking](networking.md) for protocol and playback limits.
