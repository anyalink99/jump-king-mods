# Ball King architecture

Ball King extends Jump King's native `BodyComp` pipeline. It does not replace
the game loop. Native gravity, wind, collision callbacks, teleports and modded
block triggers remain authoritative; Ball King owns only ball input, contour
motion and the impulses added by its jump and bounce mechanics.

## Runtime pipeline

`MorphPipelineContract` is the single source of truth for registration order:

1. Ball control runs before Jetpack, Casual+ or native wind.
2. Pre-movement captures the velocity consumed by native collision.
3. X collision is observed after native resolution.
4. Teleports are observed after native teleport handling.
5. Y collision is observed after native resolution.
6. Position-cap observers bracket the native cap.
7. Continuous ball contacts may suppress bump audio before it plays.

Changing this order is a behavioural change. Update its characterization test
and manually verify ordinary movement, both ball modes, bounce, buffered jump,
teleports and checkpoints together.

## State ownership

`MorphController` is split by phase rather than by a second hierarchy of
behaviours:

- `NativeMovement` observes the native collision frame;
- `Movement` updates sticky and rolling contact;
- `Jumping` owns surface, coyote and double-jump impulses;
- `Transform` changes the hitbox and restores saved ball state;
- `Bounce` owns fall tracking and bounce sequences.

Mutable values live in three explicit lifetime objects:

- `MorphSurfaceRuntimeState` — current contour, normal and rolling momentum;
- `MorphFrameRuntimeState` — transient native-frame samples and teleport carry;
- `MorphJumpRuntimeState` — jump, fall and pending-bounce continuity.

Do not add another controller boolean until its owner and reset boundary are
clear. A value sampled before native movement belongs to frame state; a value
that survives a frame must belong to surface or jump state.

## Collision and contour model

`MorphSurfaceFollower` queries nearby blocking `IBlock` instances.
`MorphContourBuilder` expands their polygons into ball configuration space and
builds the exterior union. `MorphContourGeometry` selects and traverses that
union. `MorphBlockGeometry` translates game blocks into polygons.

`IBlock.Intersects` remains authoritative for whether something is solid.
Teleport, checkpoint and group-trigger blocks must not be promoted to solids.
Native collision calls and shared `SlopeBlock` instances are preserved. Ball
contours are a separate interpretation: `BottomLeft` uses its declared
south-west-facing diagonal. The installed game's `MakeLines` constructs
`BottomLeft` with the same triangle as `BottomRight`, despite their different
normals. Ball King corrects only its contour, never the shared native block.
Unknown blocking blocks use `GetRect()` unless their mod registers exact
polygon geometry.

This distinction must survive the move to shared Runtime geometry. A consumer
must request the active actor's collision/contour rules explicitly; installing
Ball King must not repair slopes for an untransformed king, native Warp or a
native-player solver. A ball contour is not a substitute for the ball's entire
collision pipeline, which still invokes native collision and block callbacks.
Geometry caches and simulations must distinguish the selected rule profile.

### Custom collider geometry

A mod with a non-rectangular custom blocking collider can register a provider:

```csharp
IDisposable registration = BallKingGeometryApi.Register(
    delegate(IBlock block, out Vector2[] vertices)
    {
        vertices = null;
        CustomBlock custom = block as CustomBlock;
        if (custom == null)
        {
            return false;
        }
        vertices = custom.GetWorldPolygon();
        return true;
    });
```

Vertices are world-space polygon vertices. Return `false` for blocks the
provider does not own. Dispose the registration when the supplying mod unloads.
The newest matching provider wins. Invalid or non-finite polygons fail clearly
instead of silently corrupting the physics state.

## Compatibility contracts

`JumpKingContract` validates all required private game members as one report.
The build runs this validation against the installed Jump King assembly.
The runtime publishes typed `player.form:1:0`, `IPlayerForm` and ordered body
phases. First-party consumers no longer rescan assemblies or call a reflection
state API. Generic third-party geometry discovery remains supported.

## Regression policy

The focused suite covers motion policy, custom geometry, the declared native
pipeline and the Moulding Manor contour corpus. The corpus has a 60-second
guardrail to catch catastrophic contour-performance regressions; normal local
runs are substantially below that budget.

Before changing physics:

1. keep the existing Moulding Manor counters stable unless the intended model
   change is documented;
2. run Ball King, Casual+ and Jetpack focused checks;
3. manually verify sticky and non-sticky transitions, slope release, buffered
   jump, both bounce heights, teleport/checkpoint momentum and save restore.
