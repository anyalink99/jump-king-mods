# Large maps

[Handbook](authoring.md) · [Parameter reference](parameter-reference.md)

## Native topology

Screen capacity follows the compiled collision atlas. The stock 13 by 13 atlas
has 169 slots; a larger atlas may have unused padding after the authored screens.
Changing Options.expectedScreens does not create geometry.

The optional `props/mega-mapping-expansion/map.xml` describes authored content:
its MapLayout root has version="1", screens and atlasSide. Each SideLink has
one-based screen and target values and side="left" or "right". Duplicate sides,
self-links, unknown fields and out-of-content targets are rejected.

With map.xml present, a nonzero expectedScreens must equal its authored screen
count, and scene objects cannot address padding. Without it, expectedScreens is
a lower-bound check against the native screen array. Rebuild the sidecar whenever
geometry changes.

Stock RGB side-teleport markers address targets 1..255. SideLink records use full
integer targets and require JK Runtime and Mega Mapping Expansion. Document both
as Workshop dependencies; map metadata cannot install or enable them.

At run start MME updates native TeleportLink arrays. Native single-link behavior
is preserved: if only one side is enabled, it serves either edge. Declare both
sides when they must lead to different screens. Unload restores links still
owned by the module; restart reapplies them. Scene hot reload does not rebuild
collision or topology. The Runtime mechanic is `mega-mapping.side-links`.

## Resource budget

PNG atlas pages must have equal dimensions divisible by their declared rows and
columns. The scene compiler and runtime enforce a 512 MiB decoded base scene
texture budget, including compiled vectors and every PNG page.

This is not total process memory. Native content, render targets, silhouettes,
meshes, decoder buffers and hot-reload overlap need additional space. Reuse assets
across screens and keep each texture within the target GPU's dimension limits.
A successful compilation does not guarantee an FPS floor.

## In-game checks

Test a compiled level with the current mods enabled. In a native debug game,
request topology validation for that level:

```powershell
.\mods\mega-mapping-expansion\tools\preview.ps1 -LevelRoot C:\Maps\MyMap -TopologyCheck
```

The check uses loaded screens, native teleport execution and the installed save
codec. It restores the player position and removes its own temporary save.
Results are written to
`props/mega-mapping-expansion/preview/topology-result.xml`.
Repeat after an in-game restart and a fresh process launch. The debug game must
be active for a paused startup barrier to complete.

Upload the compiled playable level folder through Worldsmith. Validate its
dependencies and assets separately; topology checks do not prove route completion.
