# Map requirements and restrictions

[Handbook](authoring.md) · [Native workflows](native-workflows.md)

JK Runtime owns map policy. It also works on maps without MME scenes. Put
`jk-runtime/policy.xml` directly under the playable level root (next to `props`,
not inside the MME scene directory). No editor or per-player settings rewrite is
needed. The policy is read before packaged map callbacks and remains fixed until
the world is unloaded. Scene reload does not reload policy; leave/reopen the map.

```xml-policy
<MapPolicy version="1">
  <Module id="mega-mapping-expansion" mode="require" minimum="0.5.0" reason="Map presentation and narrative" />
  <Module id="example.optional-flight" mode="suspend" reason="This route uses native jumps" />
  <Mechanic id="example.flight" reason="This route uses native jumps" />
  <Assembly id="ExampleConflictingMod" reason="Its global collision patches conflict with this map" />
</MapPolicy>
```

`example.*` names above are illustrative, not built-in mod IDs. Find real IDs in
Runtime module/mechanic diagnostics. Assembly rules use the managed assembly
simple name, not a DLL path, Workshop ID, display name or version-qualified name.

| Entry | Meaning |
| --- | --- |
| Module, mode=require | Module must exist, meet optional minimum version, survive dependency resolution and activate successfully |
| Module, mode=reject | A registered module is an explicit conflict; ask the player to disable it |
| Module, mode=suspend | Skip map callbacks, preparation and activation only when the module opted into safe map suspension; an absent module is fine |
| Mechanic | Cooperative prohibition by stable mechanic ID; the Runtime registry rejects forbidden registrations |
| Assembly | Reject a loaded foreign assembly; no arbitrary unpatching or forced unloading |

All entries require a nonempty `reason` of at most 256 characters. IDs are unique
within each entry kind. At most 256 entries total, 64 KiB XML, no DTD/external
entities. `minimum` belongs only to Module require. Unknown fields, duplicate
declarations and contradictory modes for one module are errors. No policy file
means no restrictions; malformed policy never means unrestricted mode.

Suspension is deliberately opt-in. Runtime cannot undo a foreign mod's static
initializers or prove that its unmanaged patches are removable. A nonparticipating
module produces a conflict message rather than pretending it is disabled. A denied
mechanic does not search/unpatch arbitrary DLLs: use a Module or Assembly rule if
the whole mod conflicts. This is compatibility coordination, not a security sandbox.
Map exit clears all restrictions without changing user preferences.

## For module authors

Set `RuntimeModule(..., MapSuspendable = true)` only if all map-specific resources
are owned by Runtime scopes/contexts and skipping every map callback is safe.
For manual registration set `ModuleDefinition.MapSuspendable` before Register;
it becomes immutable after registration. Native bootstrap/discovery and menu
callbacks still exist; never install permanent gameplay changes there.

Before acquiring resources for an optional mechanic, query
`MapPolicy.AllowsMechanic(id)`. `MechanicReason(id)` explains a restriction;
`Describe()` returns a detached diagnostic summary. These are game-thread APIs.
If denied, do not install that optional mechanic. If it is essential to the module,
report a conflict instead of partially activating it. The registry's rejection is
a final guard; it does not by itself undo work done outside a tracked scope.

No busy polling, foreign assembly scans per frame, or graphics work is added.
Policy is checked at world/attempt boundaries; assembly inventory is inspected
only if the policy declares Assembly entries. See Runtime's map-policy guide for
the SDK contract.
