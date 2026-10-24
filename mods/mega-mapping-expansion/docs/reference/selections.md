# Select drawing, surfaces and cutscene actors

[Handbook](../README.md) / [Reference](README.md)

Selections choose existing objects; they don't create them. Tokens are case-sensitive and separated by semicolons. Use IDs and tags from the [inspector](inspector.md) rather than guessing a native surface identity.

## Capture selection

| Token | Selects |
|---|---|
| `panel` | Scene drawing with component ID panel |
| `tag:lamps` | Scene drawing tagged lamps with Component tags="lamps" |
| `native.background` | Native background drawing |
| `native.midground` | Native midground drawing |
| `native.foreground` | Native foreground drawing |
| `actor:player` | Live gameplay player |
| `actor:ending.king` | Native cutscene King; corresponding roles listed below |

An empty capture selection copies the available pre-UI world frame. An explicit selection such as `actor:player;tag:lamps` draws those objects into a transparent screen-sized target. The output sprite normally stays at 0,0. Choose a capture stage at or after its selected scene drawing and draw the output afterward. Recursion is invalid.

Ending roles are `king`, `babe`, `cherub`, `hanging_babe`, `crow`, `gargoyle` and `bird`. Available actors depend on the ending and current step. Captures draw their current state, not another simulation.

## Surface-sensor selection

Use an exact surface ID, `type:Full.Namespace.BlockType`, or `tag:name`. Empty selection accepts all observed surfaces. A surface sensor only sees contacts supplied by native movement observation. A picture tagged as a platform isn't automatically a collision surface.

Use `phase=contact` for these contacts. The transform's screen filters the contact's screen, including retained contacts from before a teleport; 0 accepts any observed screen. `minimumSpeed` filters contact speed. [Contact feedback](../scenes/contact-feedback.md) explains blocking impact versus nonblocking entry.

## Light occluders

A light's `occluders` selects **receiver** components whose `mode=occlusion`. Use their IDs or `tag:name`. These are invisible prepared contours. An empty selection selects no explicit contour. Native collision isn't automatically copied into them.

## Following an ending actor

For positions, use input source `actor:ending.king.position` and test `.present` or availability. `actor:ending.king` by itself is a drawing selector, not a position signal. [Ending choreography](../integration/endings.md#follow-cutscene-actors) explains actor lifetime after the gameplay player is destroyed.
