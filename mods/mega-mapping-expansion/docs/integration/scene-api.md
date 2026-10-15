# Public scene API

[Handbook](../index.md) / [Integration](index.md)

Use the public API when another mod needs scene observations or prepared effects. Most map behavior can stay in XML. The API exposes detached typed values and leases; it doesn't expose native entities, mutable scene definitions or GPU handles.

Reference MegaMappingApi.dll, assembly version 2.0.0.0. A Runtime module requires capability `mega.mapping.scene:2:0`. Don't reference MME's internal implementation assembly just to read charge or activate a map effect.

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

SceneValue kinds are Number, Integer, Boolean, Vector2, Color and Text. Use Number/Integer/Boolean/Vector/Color/String factory methods and ReadVariable's Kind rather than guessing how a value is stored. Vector coordinates use X/Y; colors use X/Y/Z/W in 0вЂ“1; Text uses Text. Constructors reject nonfinite numeric values. Parse takes the declared kind and its textual representation.

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

WriteVariable publishes the variable's signal and updates dependent bindings. It doesn't synthesize the cause-bearing `variable:<id>` event produced by rule/tree actions. Emit an explicit domain event when an API-driven change should also trigger a reaction.

## Restore and shared worlds

Restore changes generation and invalidates old leases. Disposing a pre-restore lease can't cancel an effect reconstructed from restored state. State restoration doesn't replay historical event actions or sounds. Inspect current state again rather than assuming an old handle still represents the active epoch.

Follower worlds expose read-only observations/inspection and reject local effects, variable writes and event emission. They consume the authoritative scene state instead of running duplicate local logic or writing personal saves. Authoritative availability and identity must match before applying shared packets.

## Separate discovery contracts

PixelCollisionFactories remains a separate public discovery API for finer-resolution mod blocks. It isn't part of IMappingScene, and slope aliases preserve original factory-produced block types. See [custom block contracts](../maps/collision.md#custom-block-contract).

ChargePhase/ChargeSample are detached charge evidence types; the active scene also publishes charge through IMappingSignals. Don't use an instrument's private component as a substitute for the charge observation contract. API IDs and assembly versions are independent of the MME release number: MME 1.0 uses scene capability major 2.
