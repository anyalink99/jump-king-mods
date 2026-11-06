# Choose what keeps moving during pause or an ending

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

Use `Gameplay` time for effects that belong to active play. Use `Presentation` for an effect that should continue into an ending. Choose the clock on each time-dependent part of the scene.

## Clock behavior

| Situation | Gameplay time | Presentation time | Rules and trees |
|---|---|---|---|
| Normal play | Advances | Advances | Run |
| Native pause or Runtime UI | Stops | Stops by default | Held |
| Pause with the selected view's `animateWhilePaused=true` | Stops | Visual animation may advance | Held |
| Completed/active ending | Stops | May continue | Presentation logic can continue during the active ending |
| MME checkbox disabled | Stops | Stops | Held |

Camera/audio pause still follows native and Runtime UI. `animateWhilePaused` does not make them run through a menu.

## Set the clock in the right place

| Where | Accepted clock spelling |
|---|---|
| Graph Node, Rule, Effect, Tree | `Gameplay` or `Presentation` |
| Sprite, emitter, stamp, field components | `gameplay` or `presentation` |
| Recipe argument | Match the recipe's argument and the parts it feeds; see [recipes](recipes.md) |

A Presentation tree does not change its effect's clock automatically. If a tree waits one Presentation second but starts a Gameplay effect after completion, the effect won't advance. Apply the clock to each integrator, effect, animation, particle stream or field that needs it.

## Try it

1. Open [animation lesson 3](../../examples/lessons/03-animation/README.md).
2. Its sprite uses `clock=presentation`. Launch, then pause: the digit stops by default.
3. To deliberately animate during pause, add a global `view` component with `animateWhilePaused=true`, save and reload. [Screen presentation](views.md) shows how to configure one.
4. Remove that setting after the experiment if the map should freeze normally in menus.

Durations use seconds on their selected clock. Zero-time inspection/refresh does not integrate movement again or replay event actions. [Saves](state.md) covers restoring clocks and sequences.

During native completion, MME releases gameplay observations and keeps the scene's presentation resources until restart or world exit. Ending actors replace the gameplay player; use their observations or a retained capture. [Ending presentation](../integration/endings.md) explains the layer below credits, stage choices and pre-teardown snapshots.
