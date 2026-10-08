# Small effects with Runtime

Use these helpers when their defaults fit your mod. Existing native patches,
custom audio and low-level presentation APIs still work; adopting one helper
doesn't require migrating the rest of a mod.

## Build a short trail in your mod

Runtime supplies [appearance sampling, capture and visual composition](player-presentation.md).
The effect's emission rate, lifetime, colours and history belong to the mod.
The compiled [PresentationExample](../examples/PresentationExample.cs) shows a
small local implementation with eight echoes and a 0.18-second fade. Copy and
adapt it; it isn't a public Runtime type.

Own the example in your actor scope. Call `Capture(player)` at the start of an
action with an idle SpriteBatch, then `Emit(player)` to add positions using the
same frozen image. Call `Update(delta)` once per native update and `Draw()` from
your visual contribution while SpriteBatch is active. Drawing doesn't age the
effect, so multiple camera passes remain safe. Clear it on state restore and
dispose it when its owner ends. The example releases its image after the last
echo expires or the graphics device invalidates it.

The sample saves the geometry anchor and custom projection with the image.
Projection providers must remain usable for the effect's lifetime. Outfit
changes don't alter a frozen image. Run all calls on the game thread. In MGE,
Dash owns its own trail implementation on these same public APIs.

## A reusable sound

During `OnWorldReady`, own
`PreparedSound.FromResource(typeof(Module).Assembly, "Example.jump.wav")` in the
world scope. `FromStream` is available too; it copies the remaining bytes and
leaves the caller's stream open. Initial decoding happens during preparation.

At activation, track `sound.BindToAttempt("example.mod")`. This optional binding
follows native pause, stops on state restore and stops on attempt teardown.
The prepared resource survives restarts. Without a binding, call `SetPaused`
and `Stop` yourself. Only one attempt binding can own a sound at a time.

`Play()` restarts a single voice. `Stop()` cancels it. A Play requested while
paused starts on resume; a stopped or naturally finished sound isn't revived by
later pause observations. `Volume` and `IsLooped` survive voice replacement.
Native SFX preferences apply by default; select a different `SoundType` when
loading music or ambience.

The helper retains encoded bytes. Restarting a voice that XAudio hasn't finished
stopping replaces it and decodes those bytes again, without file IO or waiting.
It isn't a polyphonic mixer or a streaming music player. Use separate prepared
sounds for independent voices, or keep specialised audio code where needed.

## Run between native player components

Prepare `PlayerUpdates` in the world scope, then track one registration:

```csharp
context.Track(PlayerUpdates.Register(player, "example.mod",
    PlayerUpdatePhase.AfterInput, delta => ReadFreshInput(delta)));
```

`BeforeInput` runs after preceding components, including the body, immediately
before the input slot. `AfterInput` runs immediately after that slot and before
the following components, including the behaviour tree. Callbacks run even when
the body or input component is disabled, so a suspended transition can finish.
They run only when the native player's component update runs; there is no extra
timer or unpaused update loop.

Runtime validates body/input/tree order and preserves every native component and
its relative order. Registrations sort by ascending order, then owner ID. Use
one registration per owner and phase. Registration/removal changes a callback
list, so callbacks can dispose themselves without changing native enumeration.
New registrations don't enter a phase already being dispatched. Gameplay
exceptions propagate and populate `LastError`; they aren't silently ignored.

Own registrations at activation and release them at attempt teardown. Native
player destruction also releases them. These hooks require one loaded Harmony 2
engine, as the presentation bridge does. Unrecognised component topology is
refused. The service doesn't coordinate foreign patches that replace or skip the
native update loop. It doesn't acquire movement ownership or suspend components
for you; keep using `PlayerControl` and `ComponentSuspension` when necessary.

The compiled [CommonEffectsExample](../examples/CommonEffectsExample.cs) plays
a prepared sound on jump. Read lifecycle/preparation once, then use the
service guide for the job at hand; geometry and simulation aren't prerequisites.
