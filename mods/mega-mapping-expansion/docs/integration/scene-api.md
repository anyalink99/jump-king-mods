# Public scene API

[Handbook](../README.md) / [Integration](README.md)

This page is for C# mod developers. For a map-only reaction, use [XML rules and bindings](../scenes/behaviors.md). Use the public API when another mod needs scene observations or prepared effects. Most operations expose detached typed values and leases. The optional media interface borrows explicitly supplied graphics/audio resources.

Reference MegaMappingApi.dll, assembly version 2.0.0.0. A Runtime module requires capability `mega.mapping.scene:2:0`. Don't reference MME's internal implementation assembly just to read charge or activate a map effect.

Ship `MegaMappingApi.dll` beside the consumer's package DLL and pass it to the
Runtime packager with `-References`. The manifest records its identity and hash;
the packager doesn't copy the referenced file into your release folder.

## Resolve and own an effect

The included [module example](../../api/ModuleExample.cs) is a complete Runtime consumer:

```csharp
using JKRuntime;
using JKRuntime.Modules;
using MegaMappingExpansion.Api;

[RuntimeModule("example.lantern", "Example Lantern",
    Requires = new[] { "mega.mapping.scene:2:0" })]
public static class ExampleLantern
{
    [OnLevelStart]
    public static void Start(ModuleContext context)
    {
        IMappingScene scene = context.Require<IMappingScene>("mega.mapping.scene");
        if (!scene.Available) return;
        context.Track(scene.Activate(context.ModuleId, "pulse"));
    }
}
```

The map must declare an effect named pulse. Activate doesn't create an arbitrary effect/resource definition during play. Track its ISceneEffect in the caller's attempt scope; disposal removes only that activation. The lease exposes Id, Active and RemainingSeconds. InspectEffects supplies the owner/definition; the scene exposes Generation.

This is an activation example, not an automatic hot-reload rebinding service. A replacement scene has a new signal hub. Consumers that remain alive across reload must detect scene/generation changes, dispose old subscriptions, resolve the current scene and subscribe again.

## Scene operations

| IMappingScene member | Use |
|---|---|
| Available, Generation | Check active availability and the state epoch |
| Signals | Access read-only observations and event subscriptions |
| Activate(owner, effectId) | Acquire an owned prepared effect lease |
| ReadVariable(id) | Read a declared typed variable |
| WriteVariable(id, value) | Write that same declared type, under current world permissions |
| Emit(id, source, actor, screen, payload) | Queue a custom event with typed payload |
| InspectComponents() | Detached IDs, kinds, tags, properties, effective values and owners |
| DescribeProperties(component) | Types, defaults, units, bounds, bindability, reference/impact and choices |
| InspectEffects(), InspectErrors() | Active effect descriptions and bounded diagnostics |

SceneValue kinds are Number, Integer, Boolean, Vector2, Color and Text. Use Number/Integer/Boolean/Vector/Color/String factory methods and ReadVariable's Kind rather than guessing how a value is stored. Vector coordinates use X/Y; colors use X/Y/Z/W in 0–1; Text uses Text. Constructors reject nonfinite numeric values. Parse takes the declared kind and its textual representation.

## Borrow existing media

Require `mega.mapping.scene:2:1` for MME 1.5's `IMappingMedia`. Reference and package `MegaMappingMediaApi.dll` alongside the existing `MegaMappingApi.dll`. The media interface has its own assembly so a previously loaded scene API can't hide the new contract. Both remain independent of MonoGame. Cast the resolved `IMappingScene` to `IMappingMedia` and call `Register(source, texture, pause, stop)` during activation. `texture` is a native `Texture2D`, or null for audio only. `pause` is an `Action<bool>` and `stop` an `Action`; supply both, or neither for an image-only source. MME checks the native texture type and dimensions at registration. Keys are case-sensitive, up to 128 characters; one scene can borrow 64 sources. Duplicate keys fail instead of silently replacing another owner.

A material's structural `source` field selects the registered texture instead of an asset or capture. A sprite draws its native dimensions; its usual transform, origin and material properties apply. The provider can update that same texture on the game thread. MME never disposes it or advances a video decoder. An absent/disposed source draws nothing. A material cannot select both `source` and `capture`.

A `media` component selects the same `source` and has a bindable Boolean `active`. While active, MME owns pause/resume edges for that source's existing audio; it never calls Play or seeks. The provider must resume only a voice it paused itself. Deactivation, scene release, disabling MME, or ending cleanup stops an adopted voice once. A source that was never activated receives no stop call. Source lease disposal unregisters only that registration, including after the scene has been replaced.

Keep the registration lease and provider resources in an appropriate owned scope. To carry music through victory, use world-owned resources and retain the registration beyond gameplay attempt teardown; registering after activation does not permit starting playback during preparation. Release the registration before disposing the provider texture/audio. Registrations are tied to one scene, aren't restored by snapshots and must be renewed after scene reload. Borrowed media doesn't add network transport or serialize native playback positions.

## Observe without treating missing evidence as zero

IMappingSignals exposes Available, Generation, Read, Subscribe and SubscribeEvent. Read returns SignalSample with Id, Value, Quality and Sequence. Quality is Unavailable, Estimated, Sampled or Exact. Subscriptions return IDisposable. Keep callbacks short and track them in an owned scope.

```csharp
SignalSample release = scene.Signals.Read("charge.releasedFrames");
if (release.Quality == ObservationQuality.Exact)
{
    double frames = release.Value.X;
    // use corrected release evidence here
}
context.Track(scene.Signals.Subscribe("charge.heldFrames", sample =>
{
    if (sample.Quality == ObservationQuality.Unavailable) return;
    // update a small consumer; don't perform IO or build assets here
}));
context.Track(scene.Signals.SubscribeEvent("jump", sample =>
{
    string actor = sample.Actor;
    int screen = sample.Screen;
    // payload positions belong to this event, not a later player pose
}));
```

This snippet belongs inside an active module callback with the resolved scene/context. [The API-only consumer](../../api/SceneExample.cs) demonstrates compilation without native game/GPU dependencies. SceneEventSample contains Id, Source, Actor, Screen, Sequence, Time and a detached Payload array. Payload entries are typed SignalSamples, each with its own evidence quality.

Calls and dispatch are game-thread scoped. Don't invoke the scene from a worker or keep a native object obtained elsewhere inside its payload. Event dispatch is batched and bounded; emitting another event queues it rather than recursively executing unbounded callbacks. A failed listener is isolated and reported. Writing a variable isn't permission to mutate native gameplay physics.

WriteVariable publishes the variable's signal immediately; dependent bindings compose on the next scene tick. Final result capture also composes pending writes before freezing its rows, without advancing clocks. It doesn't synthesize the cause-bearing `variable:<id>` event produced by rule/tree actions. Emit an explicit domain event when an API-driven change should also trigger a reaction.

## Restore and shared worlds

Restore changes generation and invalidates old leases. Disposing a pre-restore lease can't cancel an effect reconstructed from restored state. State restoration doesn't replay historical event actions or sounds. Inspect current state again rather than assuming an old handle still represents the active epoch.

Follower worlds expose read-only observations/inspection and reject local effects, variable writes and event emission. They consume the authoritative scene state instead of running duplicate local logic or writing personal saves. Authoritative availability and identity must match before applying shared packets.

## Separate discovery contracts

PixelCollisionFactories remains a separate public discovery API for finer-resolution mod blocks. It isn't part of IMappingScene, and slope aliases preserve original factory-produced block types. See [custom block contracts](../reference/collision.md#custom-block-contract).

ChargePhase/ChargeSample are detached charge evidence types. ChargeSample includes `MaximumFrames`, `NativeChargeSeconds` and `ChargeMultiplier`, captured with the charge so restore retains the surface-dependent limit; the active scene also publishes charge through IMappingSignals. Don't use an instrument's private component as a substitute for the charge observation contract. API IDs and assembly versions are independent of the MME release number: MME 1.0 uses scene capability major 2.
