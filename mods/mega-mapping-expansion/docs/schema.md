# Schema and documentation maintenance

[Handbook](authoring.md) · [Generated fields](../reference/PARAMETERS_GENERATED.md)

The expanded XML model and SceneProperties registry are authoritative. The model
owns XML names, types and constructor defaults; the property registry owns the
supported live effect edits, ranges and selectors used by effects/API/inspector.
The compiler exports both rather than maintaining another editor-only protocol.

- [Generated parameter tables](../reference/PARAMETERS_GENERATED.md): every serialized
  field and its actual constructor default; live-edit bounds where available.
- [Machine-readable catalog](../reference/scene-parameters.xml): the same data for
  authoring tools. A member marked reload is not an effect-editable field.
- [Expanded XSD](../reference/scene-expanded.xsd): serializer structure after source
  Includes, Objects and shared defaults have expanded.
- [Parameter handbook](parameter-reference.md): units, relationships and renderer
  limits. Cross-field/resource rules remain in the scene validator; XSD alone is
  not a replacement for validation.

`ObjectDefinitions`, `Objects`, Include, Templates, Materials and LayerGroups are
source compiler syntax, intentionally absent from the expanded XSD. Read
[Objects](objects.md) and the handbook for those constructs. Third-party editors
must preserve source files rather than replacing them with expanded cache output.
These contracts are independent of the map editor.

From the repository root after a mod build:

```powershell
& 'build/mega-mapping-expansion/_INTERNAL/SceneCacheCompiler.exe' --validate 'path/to/level'
& 'build/mega-mapping-expansion/_INTERNAL/SceneCacheCompiler.exe' --reference 'path/to/reference'
& 'build/mega-mapping-expansion/_INTERNAL/SceneCacheCompiler.exe' --schema 'path/to/scene-expanded.xsd'
.\mods\mega-mapping-expansion\tools\check-docs.ps1
```

The build regenerates reference artifacts and copies them into the authoring kit.
Do not edit generated files. Docs checking traverses handbook links and compiles
complete `xml` scenes, native ending examples and `xml-policy` fences. Fragment
fences are explicitly partial, not advertised as tested complete scenes.

## Release checklist

1. Add/change the model field and validation; use the shared live-property
   registry if effects may edit it. Keep draw-resource changes structural unless
   a verified invalidation path exists.
2. Document units, default, limits, layering, pause/restart/save behavior and
   performance cost. Provide a small complete scene or extend a validated fixture.
3. Build MME (including Runtime and focused tests); run graphics integration for
   native rendering/hooks and source checks for affected maps.
4. Run docs checks for repository and exported kit. Rebuild maps whose cache
   compiler identity changed. Inspect package dependencies; do not ship test data
   or game assemblies as runtime payload.

Generated field coverage is exhaustive for the serializer model. It does not
mean every possible combination has been playtested, every font covers all
languages, or every foreign mod is compatible. Report those boundaries precisely.
