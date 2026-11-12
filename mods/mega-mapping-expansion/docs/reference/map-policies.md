# Map policy rules and module cooperation

[Handbook](../README.md) / [Reference](README.md)

For the usual map requirement, follow [Require MME](../maps/map-policies.md).
These rules are read from `jk-runtime/policy.xml` when a world loads. They stay
fixed until it unloads. Scene reload doesn't reread them.

## Policy entries and limits


Find real module and mechanic IDs in Runtime's diagnostics. Assembly rules use the managed assembly
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
module produces a conflict message when it cannot be disabled. A denied
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
