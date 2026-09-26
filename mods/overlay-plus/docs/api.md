# Overlay provider API

Reference `OverlayPlusApi.dll` and the game's MonoGame assembly. Register on the
game thread at normal module activation and dispose the lease on deactivation.
Keep the source ID and callbacks unchanged while registered. IDs must be globally
namespaced. `Sources()` and `Generation` support discovery without loading foreign
menus or changing another mod's settings.

```csharp
var lease = OverlayRegistry.Register(new OverlaySource {
    Id = "example.counter",
    Name = "Example counter",
    Available = () => counter != null,
    Text = () => counter.Value.ToString(),
    SetNativeVisible = visible => drawNativeCounter = visible
});
context.Track(lease);
```

Callbacks run on the game thread. Text and Available are sampled after native
Update. An optional `Draw(SpriteBatch, Rectangle, float opacity)` replaces text:
use the supplied **already active** batch and logical rectangle, premultiplied
colors and the requested opacity. Do not Begin/End, change targets or device
state, perform I/O, mutate simulation, register providers or open menus in Draw.
Overlay+ owns scale and clipping. The registry does not perform cross-thread
marshalling; background registration is unsupported.

Native visibility is suppressed only after the relocated source draws
successfully. It is restored when ownership ends, availability disappears,
drawing fails, the layout hides that source, the attempt ends or the lease is
disposed. A failing provider is isolated for the current registration. If several
widgets display one source, it is sampled once and can draw in several places.

The built-in `jump-percent.native` adapter captures text calls inside Jump%'s
specific draw scope, including Subframe Charge's postfix. It does not invoke the
foreign draw a second time, copy gameplay state, move render targets or capture
unrelated HUD calls. Missing optional assemblies simply omit their integration.
Replays links use replay IDs and remain optional; deleting a recording does not
delete run history.
