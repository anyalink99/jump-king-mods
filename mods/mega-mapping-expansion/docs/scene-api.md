# Mapping scene API 1.0

The scene API retains its **1.0 ABI**; current MME requires **JK Runtime 1.30 or newer 1.x**. Reference
`MegaMappingApi.dll` for `MegaMappingExpansion.Api`, plus `JKRuntime.dll` for
module registration. The contract assembly identity is 1.0.0.0; it contains no
native hooks, textures or implementation types. Do not reference the embedded
MegaMappingExpansion.Module implementation.

## Resolve a declared capability

```csharp
using JKRuntime;
using JKRuntime.Modules;
using MegaMappingExpansion.Api;

[RuntimeModule("example.lantern", "Example Lantern",
    Requires = new[] { "mega.mapping.scene:1:0" })]
public static class ExampleLantern
{
    [OnLevelStart]
    public static void Start(ModuleContext context)
    {
        IMappingScene scene = context.Require<IMappingScene>("mega.mapping.scene");
        if (!scene.Available) return; // Map has no Mapping scene.
        context.Track(scene.Activate(context.ModuleId, "carried-lantern"));
    }
}
```

This example targets the lantern definition in the [behavior cookbook](behaviors.md).
Alternatively call `Apply` with an EffectDefinition, using declared target IDs
and preloaded LightTemplates. `sdk/SceneExample.cs` in the exported authoring kit
is a standalone consumer compiled against the public contract and executed by
focused checks. Package your module with Runtime's SDK package tool and include
the exact compatible MegaMappingApi.dll reference in `-References`; the loader
validates dependency bytes and identity. Do not distribute JKRuntime.dll again.

## Contract facts

| Surface | Contract |
| --- | --- |
| Thread | Resolve, inspect, mutate and dispose on the game thread |
| Availability | Capability is published for the module's level lifetime; Available may be false on maps without a scene |
| Capability lifetime | Do not retain across level teardown; stale gateway reports unavailable and rejects mutations |
| Generation | Changes on scene construction/restore; compare before reusing editor selections or leases |
| Apply / Activate | Validate the entire request, copy the definition, then commit; caller edits cannot mutate the engine |
| ISceneEffect | Active, Id, RemainingSeconds; infinity for duration 0; Dispose cancels its instance idempotently |
| Stale effect | Active=false, remaining=0, Dispose harmless after expiry/unload/reload/restore |
| Owner | Unique module ID recommended, 1–128 characters; repeat/group matching is per owner |
| InspectObjects | Detached arrays of ID/kind/screen/property/value/winning-owner; invariant XML-compatible values |
| DescribeProperties | Detached type/range/choice metadata for supported dynamic fields; unknown object throws |
| InspectEffects / InspectErrors | Detached current effects and up to 64 diagnostic messages |
| GetFlag / SetFlag | Only declared keys; exact case and string comparison; unknown key throws |
| Emit | Queued event ID, 1–128 characters; capacity 256; no synchronous recursive rule execution |
| Failure | Argument/InvalidData/InvalidOperation/KeyNotFound errors explain unsupported input, budget, context or target |
| Ordering | Priority, definition ID, owner ID, instance serial; greater priority applies later |
| Save | Only explicitly save-scoped flags; native boundary and slot limitations described in cookbook |

An accepted effect is a presentation lease, not a gameplay authority. It does
not suppress native input or own another mod's component. Distinct owner strings
provide composition, not a security sandbox against another installed mod.
Refresh/ignore leases alias one instance; do not treat them as reference counts.

The engine owns all live scene data. The source DTOs remain authoring inputs;
never change a loaded scene by reflection. Supported fields use cache invalidation
and shared attachment poses. Structural edits belong to a validated reload.

## Authoring and compatibility

All effect fields, limits, clocks, reset policies and object support are in
[Regions, attachments and scene effects](behaviors.md). This separates the
stable external contract from renderer internals. The build compiles an external
consumer, validates/caches exported examples and exports `scene-expanded.xsd`.
The schema is generated from the same serializable types; ranges and ID references
are validated by the compiler, not guaranteed by XSD alone.

Use Runtime's normal state snapshot coordination for rewind. Mapping participates
with schema version 1. Requesting an unrelated old scene snapshot is an error;
never keep a pre-restore effect lease to control the restored instance. Reacquire
the current scene, inspect it and issue a new owned request where necessary.
