# Maintaining this handbook

The public signature reference is generated from the built assembly. Semantic
contracts are authored here and must be reviewed against implementation and tests.
Generated signatures cannot explain lifetime, side effects or failure recovery.

All authored technical guides live in this `docs/` directory; `index.md` is the
entry point. `api.md` and `ui-api.md` hold service contracts, while task guides
own complete workflows and examples. Keep shared explanations in one primary
guide and link to them. The mod root retains README, changelog, Workshop copy
and any license notices. The SDK root retains its package README and generated
`PublicApi.md`; it exports authored guides under the same `docs/` paths.
The old UI changelog is an archive under `history/`; new UI changes belong in
the main Runtime changelog.

For every public service change:

1. Update its guide and route it from [the API map](api-map.md). New namespaces
   must have an explicit route in `docs/api-guides.json`; the build rejects gaps.
2. Document valid phase/thread, owner/release, input/output ownership, idle cost,
   exceptions/rollback, version requirements and unsupported cases.
3. Update the smallest compiled example and meaningful behavioral tests. Label
   incomplete snippets as fragments; never describe one as a standalone program.
4. Keep the getting-started path, SDK template and lifecycle guide consistent.
   A new phase must not leave older examples doing expensive work at activation.
5. Update diagnostic instructions where evidence can actually be obtained.
   Describe how to enable, reproduce, export, locate and disable each trace.
6. Run the documentation checks against both the source tree and exported SDK.
   Repository-only source references must be labelled paths, not broken SDK links.
7. Put historical changes in the changelog. Describe current behavior in guides;
   introduction versions are compatibility information, not alternate current APIs.

The documentation verifier checks links/anchors, exported examples, routes
for reflected public types, Current SDK declarations and UIApi/RuntimeApi member
references against compiled metadata. The exported ModEntry.cs must match the
canonical compiled UI example. Tests exercise page sessions and lifecycle/state examples. These
checks do not prove the prose is accurate: a review still compares behavior with
source, including exceptions, fallback paths and the actual callback order.

For a fast documentation-only recheck after an initial build, refresh the SDK
copies and run `python mods/jk-runtime/tools/check_docs.py --source mods/jk-runtime
--sdk build/jk-runtime/SDK --api-index build/jk-runtime/_INTERNAL/public-api.json`
as one command from the repository root. After API/code/example changes, use the
full build so the DLL, metadata, XML and compiled examples cannot be stale.
The verifier checks local Markdown targets and headings, not remote site health.

Use English for documentation and examples. Prefer task-oriented instructions,
small complete samples and explicit limitations over promotional compatibility
claims. Preserve stable API identifiers and saved-data keys. Documentation work
must not alter gameplay to make an example or statement easier to write.
