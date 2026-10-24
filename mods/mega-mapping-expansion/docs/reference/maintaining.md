# Build MME and check the handbook

[Handbook](../README.md) / [Reference](README.md)

These commands are for maintainers working in the source workspace. Map authors can [edit XML and load the map](../getting-started/worldsmith.md) without building MME or running a separate compiler.

## Build and validate

From the repository root:

```powershell
.\scripts\check-mods.ps1 -Mod mega-mapping-expansion
.\mods\mega-mapping-expansion\check-docs.ps1
.\mods\mega-mapping-expansion\check-docs.ps1 -DocumentationRoot build/mega-mapping-expansion/AUTHORING_KIT/minimal
.\mods\mega-mapping-expansionerify-authoring.ps1
```

The build runs focused source tests, regenerates the field registry/XSD, and validates the staged handbook and examples before publishing the authoring kit. The kit includes all handbook media, lessons with their assets, examples, recipe source and API material. The explicit checks above also let you validate source or an already exported kit independently.

Complete Markdown scenes use an `xml` fence followed by the map-relative output filename. A multi-file example is wrapped in `mme-example:start`/`mme-example:end` HTML comments. Native snippets use `xml-fragment`; Runtime policies use `xml-policy`. These distinctions tell the checker which files belong together and which snippets need a native surrounding map.

`verify-authoring.ps1` writes reports/proof images under `build/_work/`. With `-ExportImages`, it deliberately refreshes the checked-in lesson proof PNGs after rendering and assertions pass. Inspect them at 480 by 360 before committing. Images are renderer proofs on a blank canvas, not gameplay screenshots.

## Other focused checks

```powershell
.\mods\mega-mapping-expansionerify-construction-graphics.ps1
.\mods\mega-mapping-expansionerify-scene-api.ps1
.\mods\mega-mapping-expansionerify-ending-compatibility.ps1
```

Graphics tests exercise GPU paths. API checks verify consumer/package discovery. Ending compatibility uses the installed provider and reports its actual test result; it doesn't certify choreography. Use the selected-mod Integration tier when changing native integration or release packages. Build outputs stay under `build/mega-mapping-expansion`; ad hoc investigations belong under `build/_work/`.

## Update documentation with behavior

Update the [task guide](../getting-started/examples.md), [feature checklist](features.md), [field explanations](components.md) and relevant XML/graph/event reference. Keep exact numeric defaults in the generated catalog. Avoid a repeated release number on every page: the setup page owns general requirements; feature-specific requirements belong with that feature.

Don't claim a route is playable or a native sequence is tested from compiler validation alone. Keep source, build, renderer and final in-game checks distinct when reporting evidence.
