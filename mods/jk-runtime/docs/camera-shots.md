# Scripted camera views

Use `CameraShots` to show another part of a map without moving the player.
Runtime 2.2 / API 2.4 provides the request contract and native screen cuts.
Smooth Camera 0.13 supplies interpolation. A request doesn't enable that mod
or change the user's camera settings.

## Request and release a view

Acquire a lease during normal activation with
`CameraShots.Acquire("my.mod", priority, attemptScope)`. Publish a `CameraShot`
with a canonical world-space center, zero-based screen, smooth/cut choice,
transition duration and return duration. A screen's center is
`(240, 180 - screen * 360)`. Both durations are finite seconds in `0..60`.

Keep the lease while the scene needs the view. `Clear()` withdraws its request
without disposing the lease. Publishing again reactivates it. Scope disposal
releases the request on restart, unload or failed activation. Up to 64 leases
may exist. These methods are game-thread operations.

Higher priority wins. For equal priorities, the most recently activated request
wins. Publishing the same active lease again doesn't change its priority or
activation order. `Selected` tells the author whether its request won.
An exclusive `CameraTargets` session takes precedence over all map shots;
existing replay ownership and acquisition errors retain their original meaning.

Read requests through `TryRead(out CameraShot)`. The returned struct is a detached
snapshot. An absent request means normal framing should resume. `Id` identifies
one activation, so a renderer can start interpolation once rather than on every
property update.

## Providers

Smooth Camera registers `RegisterSmoothProvider(ready)` while its hooks exist.
Only one smooth provider may register. `SmoothAvailable` queries its current
readiness; it doesn't install anything. The provider owns easing, pause and
return-to-follow behavior. Release its registration at teardown.

Smooth Camera pauses interpolation with the native pause or Runtime UI. It cuts
between disconnected vertical screen ranges rather than sweeping through
unrelated screen numbers. A native cut shows the complete requested screen;
point framing and timed returns require the smooth provider.

## Native draw scopes

Call `NativeScreenView.Prepare(worldScope)` during preparation. It installs shared,
reference-counted draw adapters, with no active view. MME may also hold this
preparation in its prepared scene scope. Repeated callers share the hooks.

The service draws a pending shot as a native screen cut when no smooth provider
is ready. It never writes `Camera._current_screen`, teleports the body, changes
the update order or runs NPC simulation for the displayed screen.

Camera renderers can enter `NativeScreenView.Enter(screen, extraTranslation)`
inside their drawing pass. The returned `DrawScope` changes native draw queries
on that thread only. Dispose nested scopes in reverse order, including exception
paths. `ExtraTranslation` adds an atlas/side-view displacement; it isn't a body
position. A render query may see the selected screen while a worker or subsequent
simulation query continues to see the real native screen.

Prepare before using a scope. The adapter rewrites known native drawing call sites
as well as getters, since previously inlined getters can otherwise ignore a
camera override. Unexpected native call shapes reject preparation. Custom mods
that cache a screen or use their own rendering need their own integration.

These scopes don't own render targets or final presentation. Continue to use
`FrameComposition.WorldPass`, `ScreenPass` and the single presenter registration
when composing an atlas. Idle scopes and inactive requests don't render anything.

The compiled [camera shot example](../examples/CameraShotExample.cs) shows the
preparation/activation split. The example publishes a cut; replace its request
with a smooth one when the optional provider is available.
