# Packages, dependencies and data paths

A feature package is one generated native discovery shell containing its managed
implementation and manifest. The shell can be enumerated by Jump King's loader
without resolving Runtime types. Runtime later validates and loads the embedded
implementation. This is dependency ordering, not a security sandbox or assembly
unloading mechanism; replacing loaded code requires restarting the game.

## Build a package

Start with the exported SDK's `ModEntry.cs` and `build.ps1`. The implementation
must have exactly one `[RuntimeModule]` entry type. It uses attributes from
`JKRuntime.Modules`; it must not have another native `JumpKingMod` entry.
Choose stable namespaced IDs, an assembly version and a descriptive assembly
title. Method signatures and lifecycle order are in [lifecycle](lifecycle.md).

In the repository, `sdk/package.ps1 -Implementation <dll> -Output <shell> -GameDir
<game>` wraps the built packager. In a detached SDK, the template build invokes
`PackageBuilder.exe` directly. Its arguments are:

```text
PackageBuilder.exe <implementation.dll> <output-shell.dll> <game-directory> [explicit-reference.dll ...]
```

The output directory must exist. The template stages the compiler/runtime/game
dependencies for the packager; preserve that staging if customizing the script.
The repository build requires Python 3.10+ for documentation checks; building a
feature from the exported template needs the Windows .NET Framework compiler,
SDK files and installed game, without Python or repository sources.

The packager creates `module.xml` beside the implementation and embeds it as
`JKRuntime.Manifest`, plus implementation bytes as `JKRuntime.Module`. The
manifest records schema/API requirements, module/entry/assembly identity,
implementation SHA-256, capabilities, ordering and explicit references.
Regenerate it when rebuilding; do not edit checksums or lower API requirements
to force a rejected package to load. See [compatibility policy](api-policy.md).

## Capabilities and assembly references are different

`Requires`/`Provides` describe versioned **services** and their activation order.
Resolving a capability does not make an arbitrary CLR assembly discoverable.
Conversely, referencing a contract DLL does not declare a service dependency or
cause a provider to activate. Declare both when your integration requires both.

Additional contract/library DLLs can be supplied as explicit packager arguments
(repository wrapper: `-References`). They are recorded by simple/full assembly
identity, sibling filename and SHA-256. The packager records them; **it does not
copy them into the release folder**. Stage the intended files yourself and keep
them synchronized with the manifest. The generated shell still only references
game/BCL assemblies; implementation code may use the declared contracts.

Runtime resolves references only for implementations it owns. It first recognizes
Runtime and other owned implementations, then declared dependencies. An already
loaded assembly with the exact full identity may be reused. For a sibling file,
the resolver checks containment, existence, hash and assembly identity before
loading. It does not scan Workshop folders looking for a compatible DLL. Because
already loaded identities may be reused, the recorded file checksum is not a
guarantee of isolated or authenticated third-party code.

Avoid shipping shared feature implementations as duplicate contract libraries.
Use the provider's documented contract-distribution method, keep compatible
assembly identities and declare capability absence/version handling. The
simulation and geometry schema versions remain separate from package API version.

## Select the payload

| File | Where it belongs |
| --- | --- |
| Generated feature shell | Feature's Workshop/local mod folder |
| Required feature assets/config defaults | Feature payload at documented paths |
| Explicit contract/library dependency | Documented sibling file, if needed by the package |
| `Name.Module.dll`, `module.xml`, compiler tools | Build artifacts, already embedded where appropriate |
| Game binaries and SDK reference Runtime DLL | Compile/staging inputs, never duplicate feature dependencies |
| `JKRuntime.dll` | The one active Runtime installation |
| User settings, saves, rollback backups | Preserve during upgrades; do not replace with development data |

The ordinary template package needs only its generated `Example.dll`. Rename
the output and replace example IDs for a real release. Add Workshop item
**3793086563** as a required dependency. Keep installed copies unique and close
the game before replacing any loaded DLL. Do not install an entire SDK folder.

## Discovery and persistence

`PackageHost.Discover` consumes `ModLoader.LoadedMods`, not every installed file.
A subscribed but disabled/unloaded Workshop item is not automatically active.
Invalid schemas/API requirements, identities, checksums, duplicate IDs and bad
entry signatures surface through package/module diagnostics. Discovering again is
not a hot reload or a general retry for a rejected immutable package.

Use `PackageHost.GetDataDirectory(typeof(Entry).Assembly)` for data beside a
package. Byte-loaded implementations may have an empty `Assembly.Location`.
Keep mutable settings out of SDK/build paths and preserve corrupt originals when
recovering; see [settings and files](settings-and-commands.md).

Follow [verification and release](testing-and-release.md) for a detached build,
payload inspection and live acceptance. Use [troubleshooting](troubleshooting.md)
to distinguish discovery, preparation, activation and runtime failures.
