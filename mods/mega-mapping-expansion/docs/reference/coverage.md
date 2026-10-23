# Handbook coverage and verification

[Handbook](../README.md) / [Reference](README.md)

Coverage reviewed against the repository sources on **2026-10-22**, for MME 1.3.0. This is a documentation audit date, not a compatibility promise for future game/mod versions.

## What is covered

- Every registered component kind and field has a [purpose explanation](components.md) and generated type/default/bounds data.
- Every embedded recipe argument and exported port has an [argument reference](recipes.md); task guides show complete uses.
- Every ValueOperation has its input order and meaning in [graphs](graphs.md), with type/unit and missing-evidence rules.
- Every scene action and Rule/Effect/Set control is listed in [actions](actions.md).
- Every construction tree operation and Tree setting is listed in [trees](../scenes/behavior-trees.md). Native tree nodes and role restrictions are listed separately in [endings](../integration/endings.md).
- Global and adapter observations, event spellings, payloads, selections and actor lifetimes are documented in [observations](../observations/README.md), [events](events.md) and [selections](selections.md).
- Layouts/resolutions, aliases, pools/schemes/triggers, physics reference frames, policy, state and API contracts have their own linked chapters in [the feature checklist](features.md).

The audit used the primitive/native descriptors, scene/graph DTOs and evaluation, recipe sources, native observation/controllers, layout/transition parsers, ending parser, state handling and public API. The generated field tables remain generated; their output isn't a substitute for the descriptions.

## What the checks establish

`check-docs.ps1` follows handbook links, validates complete XML examples with the built scene compiler, parses identified native illustrative fragments/policies, and validates the supplied example folders. It runs against both source docs and the exported authoring kit. Fragments still need their surrounding native map and resources.

`verify-authoring.ps1` loads the seven lesson files into the actual scene program and renderer on an isolated graphics device. It checks visible output, area-caption membership/binding, jump count progression, action routing and saved-variable serialization. It writes proof images on an empty canvas; these are clearly labeled in the lessons. It doesn't launch a playable route or prove a native NPC/ending was staged correctly in an arbitrary map.

Native prerequisites are stated beside each worked example: existing NPC/name, item pickup, Hidden Wall definition, referenced screens, native ending resources, or artwork/audio. For a full map, test those native interactions, pause, restart and completion in the final playable package with its intended mod set. [Maintainer checks](maintaining.md) gives the commands.

## Keep it current

When a feature changes, update its task guide, field/operation contract and example together. Rebuild to regenerate the registry, validate source/kit, and exercise the changed behavior. Record game/provider compatibility from current focused checks rather than carrying an old tested combination forward as current support.
