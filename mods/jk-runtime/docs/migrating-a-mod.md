# Migrate an existing mod: LessAutoEquipping

Use the [complete buildable example](../examples/less-auto-equipping/README.md)
to follow a real migration. It adapts Zebra's
[LessAutoEquipping](https://github.com/gitAdrianK/JumpKingMoreOrLessMods/tree/e64b6d270e0a8fc1c9b95611efb7e1ac1a0223c9/LessAutoEquipping),
preserving its three gameplay patch algorithms and saved preference. Reading
this guide does not require moving an existing mod's entire UI or patch system
to Runtime services.

## What changes

| Original | Runtime example | Reason |
| --- | --- | --- |
| Native `JumpKingMod` entry | One `RuntimeModule` entry and generated discovery shell | Game discovery can enumerate the package without loading SDK types |
| Native ID `Zebra.LessAutoEquipping` | Runtime ID `zebra.less-auto-equipping` | Runtime module IDs require lowercase ASCII; saved keys and Harmony owner remain unchanged |
| `PatchAll` in native `BeforeLevelLoad` | Three explicit registrations owned by the `OnWorldReady` scope | Patches are ready before intro rewards and released on world exit or failed preparation |
| Menu attributes on a separate toggle class | Two exported factories on the module entry | PackageBuilder exports entry methods; separate factories preserve both native menu locations |
| `Assembly.Location` for preferences | `PackageHost.GetDataDirectory` | The implementation is loaded from bytes, while user data stays beside the installed shell |
| Reflection serializer and property-change subscription | A small compatible reader and atomic save on toggle | Preserve upstream booleans and file keys without duplicate subscriptions |
| Direct feature DLL | Implementation passed to PackageBuilder | Only the shell, explicit dependency and notices are distributed |

## Entry and lifetime

The original native attributes come from `JumpKing.Mods`. Packaged implementation
attributes come from `JKRuntime.Modules`; do not retain a second native entry.
The example uses `OnWorldReady(RuntimeScope scope)` because its patches touch
item-grant methods that can execute during the native intro, before normal
`OnLevelStart` activation. They need no player instance or loaded scene resource.

The scope owns an `OwnedPatches` instance before any patch is installed. Attempts
reuse that world registration; failed preparation and world exit release it.
Existing settings are loaded lazily so the main-menu toggle also works before a
world exists. Menu state does not depend on a successful gameplay activation.

This choice is specific to these patches. A geometry-loading patch would instead
need registration before native screen loading; a player controller belongs at
activation. Use the [lifecycle table](lifecycle.md) and
[preparation rules](preparation.md) to choose each operation's phase.

## Keep the patch behavior

The example retains the three upstream transpilers for world pickup, merchant
purchase and ending reward. It removes optional annotation dependencies, uses
C# 5-compatible property names and reports a missing native instruction pattern
as an error. The copied code and adaptation retain Zebra's MIT notice.

`OwnedPatches` delegates those transpilers to the one loaded Harmony engine and
tracks their removal. This does not resolve arbitrary patch conflicts, reorder
foreign patches or guarantee generic-method compatibility. The example still
references and packages Harmony; Runtime itself supplies no Harmony DLL.
Its build and tests select the reviewed 2.3.6 dependency explicitly.

The ownership ID remains `Zebra.LessAutoEquipping.Harmony`. The example rejects
the original mod when loaded alongside it, and refuses duplicate ownership of
its target methods. Removing owned patches must leave other authors' patches
intact. Both cases belong in a migration's verification plan.

## Preserve user data and menus

The upstream serializer writes a bool as `True` or `False`. Passing that old XML
straight to `XmlSerializer` would fail: its XML boolean spelling is lowercase.
The example reads both spellings and retains the same root, property and filename.
Only an explicit toggle writes a new file, using `AtomicXmlFile.Save`; failed
writes leave the current in-memory value unchanged. A corrupt file is surfaced
without replacing it with defaults.

Put menu factories on the entry class. The original toggle type remains a native
`ITextToggle`, so this example needs no UI redesign. It exports a main-menu factory
and a pause-menu factory separately. Menu creation must initialize settings safely
even when gameplay has never started.

## Build and review

Follow the [example build commands](../examples/less-auto-equipping/README.md#build).
The build runs its fixtures before packaging, then tests the actual shell in a
fresh process. It verifies a byte-loaded implementation with an empty assembly
location, data lookup beside the shell and both exported native menu attributes.

Keep existing project tooling when migrating a larger mod: add the SDK reference
and package step to that build. The example uses the SDK's C# 5 compiler for a
self-contained tutorial; Runtime does not require rewriting all existing code
to C# 5. See [packaging](packaging.md) for dependency declarations and payloads.

Automated native-method fixtures are not a full interactive playtest. Run the
example's [acceptance cases](../examples/less-auto-equipping/README.md#verification-boundaries)
before distributing a release. Source coexistence or a successful compile alone
does not establish compatibility with another mod's patches.
