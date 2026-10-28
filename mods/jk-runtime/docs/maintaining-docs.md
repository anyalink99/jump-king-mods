# Maintaining this handbook

The build generates the public signature reference from the assembly. Write the
meaning of those APIs here and check it against both implementation and tests.
A generated signature cannot explain lifetime, side effects or recovery.

Keep authored technical guides in this `docs/` directory and route them from
`index.md`. Put service contracts in `api.md` or `ui-api.md`; put complete tasks
and examples in focused guides. Keep a full explanation in its main guide;
repeat a short explanation wherever it helps the reader, then link to the details.

## Write for the reader's next step

A README should let a player install the mod and try its main feature. Put that
path before implementation details. A guide should name its task, prerequisites
and expected result before listing edge cases. References can be precise and
technical; tutorials should get one small example working first.

Summaries can appear in several places. Keep them short, use the same names and
defaults, and link to the full guide with a label that says what the reader will
find. Don't make someone follow three indexes to answer a simple question.

Describe current behavior directly. Put release history in the changelog and
test inventories in validation guides. Mention implementation details when they
help the reader use or integrate the API. Cover production and private authoring
tools don't belong in player instructions. Keep credits and license notices.

When a requirement changes, check the README, handbook entry points, examples,
SDK template and public repository templates together. Separate current source
requirements from the minimum of an older published package. Check the actual
package manifest and API version; don't infer either from assembly identity.

## Keep the repository and SDK together

The mod root holds the README, changelog, Workshop copy and license notices. The
SDK root holds its package README and generated `PublicApi.md`, while authored
guides keep the same `docs/` paths in the export. The old UI changelog remains an
archive under `history/`; record new UI changes in Runtime's main changelog.

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
copies and run `python mods/jk-runtime/build-support/check_docs.py --source mods/jk-runtime
--sdk build/jk-runtime/SDK --api-index build/jk-runtime/_INTERNAL/public-api.json`
as one command from the repository root. After API/code/example changes, use the
full build so the DLL, metadata, XML and compiled examples cannot be stale.
The verifier checks local Markdown targets and headings, not remote site health.

Use English for documentation and examples. Prefer task-oriented instructions,
small complete samples and explicit limitations over promotional compatibility
claims. Preserve stable API identifiers and saved-data keys. Documentation work
must not alter gameplay to make an example or statement easier to write.
