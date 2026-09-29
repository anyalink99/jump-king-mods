# Cosmetic particles

Runtime API 1.33 advertises `cosmetic-particles-v1`. These game-thread services
provide bounded storage, explicit simulation, shared collision indexing and
ordered sprite submission. They do not change player physics, grant equipment,
install update/draw hooks, create worker threads or advance time automatically.
The compiled [example](../examples/ParticleExample.cs) shows ownership and calls.

## Storage and clocks

Create an actor-owned `ParticleSystem<T>` with a capacity and update callback.
`T` derives from `ParticleState`; the constructor creates all reusable objects.
`Spawn()` returns reset storage or null at capacity. `Spawn(true)` retires the
oldest particle instead. `DroppedCount` and `PeakCount` expose budget pressure.
Each system has its own budget (1–65536); the caller controls the number of
systems. There is no global quality setting that silently changes other mods.

Set `Life`, position, velocity and custom fields before the next update. Update
with simulation delta in [0, 0.1] seconds; invalid/nonfinite values are rejected.
The engine ages particles, retires expired entries and calls behaviour for the
survivors in reverse order. Stored/render order stays stable. Zero delta does
nothing. Do not call from both render and simulation: repeated renders must not
advance effects. Preview/replay actors own separate systems and clocks.

`Reset()` overrides release borrowed references and call the base implementation.
They should not throw. References returned by Spawn are valid only until that
particle expires, is replaced or cleared; keeping them as permanent handles is
unsupported. Mutating the pool inside its update callback is rejected. Callback
exceptions propagate without leaving the pool locked; the owner decides whether
to clear/disable the effect. Dispose from the actor's scope. The pool never
disposes textures or other resources borrowed by its particles.

## Collision ownership

Call `ParticleWorlds.PrepareNative(scope)` in `OnWorldReady`. It records the
`runtime.particles.world` startup substage and shares one prepared index across
consumers of the same native screen array. The last preparation lease releases
the index. Attempts can reuse the world's scope. `ParticleWorlds.Current` is a
borrowed reference: do not dispose it. A detached `ParticleWorld(blocks)` belongs
to its caller and can support isolated previews or tests.

Create an actor-owned `ParticleCollisionFrame`. Each simulation update:

1. Calculate a region covering the particles' complete sweeps and support probes.
2. Call `Begin(ParticleWorlds.Current, region)`.
3. Use `Contains(point)` for point droplets or `Intersects(rectangle)` for
   conservative swept fire volumes.
4. Call `Clear()` in a finally block.

The broad phase uses 16×16 cells over all relevant screens, independent of the
camera. Point results are reused only inside that update. Exact native block
bounds are prepared once; their narrow phase still uses native `IBlock.Intersects`.
Foreign types/subclasses refresh bounds each update, and retain their own blocking
semantics. Blocks must expose enclosing bounds and side-effect-free collision
queries. This service does not execute body behaviours or model arbitrary foreign
gameplay. Geometry must remain stable during one collision frame.

Replacing native screen/block arrays or block entries invalidates use of prepared cells in the
affected query region. Missing preparation uses a region-local live index.
When a mod edits an exact native block's bounds **in place**, call
`ParticleWorlds.Current.Invalidate()` before the next simulation. Queries then
use live bounds until an explicit `Rebuild()` in a preparation phase. Invalidating
a detached world similarly enables its live fallback. This is not automatic
discovery of private-field edits. Disposed worlds must not be borrowed again.

An unloaded world is empty. Queries outside the declared region return false;
include full sweeps rather than treating that as evidence of traversable space.
Rectangle queries above 4096 pixels per dimension are rejected. Oversized blocks
use an overflow list; grid growth is capped at 131072 cells. Point scratch caching
is capped at 32768 entries. These limits bound cache storage, not the execution
time of arbitrary foreign callbacks. `NarrowPhaseQueries` counts actual block
queries since Begin; read it before Clear. Clear removes borrowed world references.

## Rendering

`ParticleBatch` is a fixed-capacity, stable-order command buffer. Add sprites with
already-resolved texture, color, source rectangle and transform, then `Flush`
inside the caller's active SpriteBatch. It does not call Begin/End, change blend,
shader, sampler or render target, reorder translucent sprites, read back pixels
or allocate textures. SpriteBatch performs the GPU batching. Use separate flushes
for different blend modes/layers. Capacity overflow rejects commands and increments
`DroppedCount`; it never silently flushes under unknown state. Flush clears queued
texture references even if drawing throws; Clear/Dispose discard queued commands.

Wardrobe+ owns water motion and authored blend/effect rules. More Items owns the
Jetpack's emission, turbulence, bounce coefficients and color stages. Both use
the shared pool, world index and submission path. No asset format or shader is
required by the particle engine; water inside Vessel King's body stays its shader.

## Validation and cost

Runtime's normal build checks stable retirement/reuse, capacity, callback failures,
native slope/water parity, moving foreign geometry, multi-screen queries, replaced
world arrays, shared scope release and oversized geometry. The warm 256-particle
fixture checks allocation bytes over 1000 updates and reports update time. It is
an isolated CPU fixture, not a full-game FPS or input-latency claim. Consumer
graphics checks cover final images and the SpriteBatch pass. World preparation
can be measured with Runtime startup diagnostics; no profiler runs while idle.