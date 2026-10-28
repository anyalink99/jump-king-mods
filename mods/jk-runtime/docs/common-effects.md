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
For an XNB sound, use `PreparedSound.FromContent(stream, game.Services, bus)`.
It retains the compiled bytes and owns its content loader. Playback and voice
replacement use memory, including when XAudio is still flushing an old voice.

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

## Advance the player at a separate cadence

API 2.6 adds `PlayerUpdates.RegisterSubsteps(player, owner, include)`. Acquire
one exclusive lease at activation and track it in the attempt context. It owns
the body, input, behavior tree, Runtime's gameplay boundaries and components
selected by the predicate. Keep the predicate pure and stable. All calls are
game-thread-only and require the native body/input/tree order.

`Advance(delta)` runs the selected components once in native order, followed by
their late updates. Delta must be finite, positive and at most 1/60 second.
Their ordinary native component slots are skipped while the lease exists.
`PlayerEntity.Update`, saves, unrelated components and existing BeforeInput/
AfterInput registrations still run only on native ticks. An exception propagates;
there is no automatic fallback. Disposal during a step stops remaining selected
work and releases ownership at the step boundary. Native destruction also
releases it. No separate timer runs when nobody owns a lease.

This is a scheduling primitive, not a fractional physics implementation. Native
movement and some timers count ticks; the owner must adapt them, check third-party
compatibility, gate pause/world authority and supply coherent input frames.
See [simulation scheduling](runtime-coordination.md#input-and-presentation) and
[input scopes](events-and-input.md#native-input-publication).

API 2.7 adds a fourth `nativeControls` predicate. Return true to keep an alternate
controller's input and tree in their native slots while the body uses substeps.
Implement `IPlayerControlObserver` on components that bracket the tree or commit
the control result; they follow its cadence. Predicates must be pure. Ownership is captured before
each native pass or substep, so a controller change during that pass cannot split
the tree from its observers.
Runtime's gameplay observers cover both the smaller body steps and alternate
controllers' native passes, so native jump callbacks retain their event frame.

Body callbacks retain native cadence unless registered through
`BodyPipeline.RegisterSubstep`, `RegisterBeforeSubstep` or `RegisterAfterSubstep`. Opt in only phases that
must observe each collision; fixed impulses and tick counters need native cadence.
SFC supplies the native delta and previous native contact snapshot to ordinary
foreign callbacks. Material velocity and collision queries still see each small
move; stateful queries require an adapter, as Conveyor's contact history does.

Wrap each fractional body pass in `PlayerPhysicsStep.Enter(body, fraction)` so
motion observers report actual displacement. Dispose scopes in reverse order;
velocities remain native pixels per tick. `MotionObservation.RefreshPhysicsAdapters`
rechecks the reviewed SFC adapters during preparation without accepting unknown
patches or previously rejected arithmetic.
