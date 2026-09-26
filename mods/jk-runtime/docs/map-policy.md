# Map policy

[Lifecycle](lifecycle.md) · [Preparation](preparation.md)

Runtime advertises `map-policy-v1` and `intro-preparation-v1`. Policy is a world-owned
declaration at `<level-root>/jk-runtime/policy.xml`. It is independent of MME and
is cleared at world exit. Read errors and conflicts are explicit startup failures.
No file means unrestricted. Edits require reopening the world, not scene reload.

```xml
<MapPolicy version="1">
  <Module id="example.renderer" mode="require" minimum="1.0" reason="Required scene renderer" />
  <Module id="example.flight" mode="suspend" reason="Map requires native jumping" />
  <Mechanic id="example.double-jump" reason="Map requires native jumping" />
  <Assembly id="ExampleForeignPatch" reason="Incompatible global collision changes" />
</MapPolicy>
```

Module modes: require (presence, version, graph and activation), reject (explicit
conflict), suspend (skip BeforeLevelLoad, world/attempt preparation and activation).
Suspension requires `RuntimeModuleAttribute.MapSuspendable=true` or a manual
`ModuleDefinition.MapSuspendable=true` set before Register. A registered definition
cannot change that contract. Absent suspend/reject targets are harmless.
Ordering-only links do not cause an optional suspended module to activate;
required capabilities it would provide are unavailable as usual.

Every rule needs a reason (1..256 characters); minimum is only for require.
At most 256 entries and 65536 XML characters. Unknown fields, duplicate IDs within
one rule kind and DTDs are rejected. Assembly IDs are simple managed names.
Foreign DLLs are never unloaded or unpatched. A mod's discovery/static initialization
is not a map callback; only opt in if it does not install permanent gameplay work.

`MapPolicy.AllowsMechanic(id)` and `MechanicReason(id)` let a cooperating mod check
before acquiring optional mechanic resources. MechanicRegistry.Register rejects
denied IDs too. `Describe()` returns detached diagnostic strings. These APIs are
game-thread only and use ordinary validated Runtime IDs. A registry declaration
cannot police an unregistered foreign patch; declare Module/Assembly conflicts
when needed. This is not a sandbox or anti-cheat mechanism.

The policy never changes saved mod settings. All graphics and player state remain
under normal module ownership. Absent policy adds no per-frame work. Foreign
assembly inventory is consulted only when an Assembly rule exists.
