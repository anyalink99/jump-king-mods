# LessAutoEquipping migration example

A buildable JK Runtime adaptation of Zebra's LessAutoEquipping, based on
[upstream revision e64b6d2](https://github.com/gitAdrianK/JumpKingMoreOrLessMods/tree/e64b6d270e0a8fc1c9b95611efb7e1ac1a0223c9/LessAutoEquipping).
This is an SDK example maintained here, not an official upstream release.
Original code is credited in [LICENSE.md](LICENSE.md); the packaged Harmony
dependency is covered by [third-party notices](THIRD_PARTY_NOTICES.md).

The setting **Disable auto-equip** keeps its original default (off) and appears
in both the main menu and pause menu. When enabled, picking up a wearable,
buying one or receiving an ending reward still grants the item, but does not
automatically equip it. Manual inventory actions are unaffected by these patches.
The existing three transpilers remain; their pattern mismatch now raises a
diagnostic error instead of silently leaving a feature unpatched.

Read the [migration walkthrough](../../docs/migrating-a-mod.md) for the changes
to entry points, menus, settings paths, patch ownership and build packaging.

## Build

Requirements: installed Jump King, an exported JK Runtime API 1.30 SDK, Windows'
.NET Framework compiler, and unmodified Harmony **2.3.6**. The original project
references Harmony 2.2.2; this example verifies these specific transpilers with
2.3.6. This is not a general compatibility claim for other mods or Harmony builds.
The build verifies the dependency hash and copies its existing license notice.

From the repository root, after building Runtime:

```powershell
.\mods\jk-runtime\examples\less-auto-equipping\build.ps1 `
  -SdkDirectory build/jk-runtime/SDK `
  -HarmonyPath mods/subframe-charge/lib/0Harmony.dll `
  -OutputDirectory build/less-auto-equipping-example
```

The exported SDK includes this whole example, including tests. From that SDK's
root, with a separately obtained Harmony 2.3.6 DLL:

```powershell
.\examples\less-auto-equipping\build.ps1 `
  -HarmonyPath 'C:\Libraries\Harmony-2.3.6\0Harmony.dll' `
  -OutputDirectory build/less-auto-equipping-example
```

Use `-GameDir` for a different game installation. The build compiles the module,
runs behavior checks against the installed game's methods, creates the discovery
shell and verifies both exported menus and the byte-loaded settings path.
It does not install anything or write to real player settings/saves.

## Try the package

The output's `UPLOAD_TO_WORKSHOP` contains `LessAutoEquipping.dll`, `0Harmony.dll`
and both license documents. Install JK Runtime separately (Workshop item
**3793086563**); do not copy SDK tools, test executables or `.Module.dll` files.

Close the game and keep a rollback copy of the original mod outside active mod
directories. Replace the original LessAutoEquipping package in its existing
folder, preserving `Zebra.LessAutoEquipping.Settings.xml`. Alternatively disable
the original and install the example in its own local mod folder, copying that
settings file there if wanted. Enable only one version. The example rejects a
loaded original entry or already installed patches with the same owner.
Workshop may replace a locally modified subscribed package on its next update.

The new Runtime module ID is `zebra.less-auto-equipping`; Runtime IDs are lowercase.
The original Harmony owner `Zebra.LessAutoEquipping.Harmony`, settings filename,
XML root and `ShouldPreventAutoEquip` key are preserved. Original `True/False`
values and the example's XML `true/false` values are both readable. Reading does
not rewrite the file; saving uses an atomic replacement and keeps a `.bak`.
Malformed files produce an error without overwriting the original.

## Verification boundaries

Automated checks cover original settings, both menus, actual patched pickup,
merchant and ending-reward methods with isolated inventory/save/audio side effects,
repeated attempts, world teardown/re-entry and preservation of unrelated patches.
A separate process discovers the shell before Runtime/Harmony, loads the embedded
implementation and checks settings beside the shell. The main Runtime build
runs these checks from the exported SDK copy.

Before distributing a player-facing release, check a real pickup, purchase and
ending reward with the setting on/off, manual equipping, both menu locations,
restart, return to menu, map change and a fresh process. These interactive checks
are separate from the automated fixtures. If a different Harmony engine is
already loaded, the bundled DLL alone does not establish which engine executes.
