# Show another part of the map

[Handbook](../index.md) / [Native game systems](index.md)

A `camera` component requests a view. Activate it with a binding or an owned
effect. It doesn't teleport the King or change the simulation's current screen.
The event that starts a shot can come from anywhere in the map.

MME 1.1 requires Runtime 2.2. Native cuts work with that pair alone. Smooth Camera
0.13 adds a timed transition to an exact point and a timed return. It must be
installed and enabled; declaring a smooth shot doesn't turn it on for the player.

## A complete screen showcase

Use this scene in a map with at least two screens. Jump on any screen to show
screen 2 for three seconds. The effect then releases the request.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="showGate" kind="camera">
      <Value name="screen" value="2" />
      <Value name="position" value="240,180" />
      <Value name="transition" value="smooth" />
      <Value name="duration" value="0.7" />
      <Value name="returnDuration" value="0.5" />
      <Value name="fallback" value="cut" />
    </Component>
  </Components>
  <Effects><Effect id="show" duration="3"><Set target="showGate/active" value="true" /></Effect></Effects>
  <Rules><Rule id="look" event="jump" once="true"><Action op="effect" target="show" /></Rule></Rules>
</MappingSettings>
```

`screen` is one-based; `position` is in that screen's local pixels. `(240,180)`
frames its center. With Smooth Camera, another point is placed at the viewport
center. Points near an unconnected map edge can expose empty space; use a screen
center when the whole room should fit.

`duration` is the time to reach the requested view. It doesn't set how long the
request remains active. The effect duration or a tree controls that lifetime.
`returnDuration` controls the return toward live tracking after release. The
normal tracker continues underneath the shot, so return follows the current
King instead of an old saved position.

## Cuts, smooth movement and disconnected rooms

`transition=cut` switches framing immediately. `transition=smooth` uses a
smoothstep transition. Pause and Runtime UI hold its elapsed time. The default
pixel snapping keeps logical output positions whole; the shot doesn't opt the
scene into fractional geometry.

When Smooth Camera is unavailable, `fallback=cut` shows the entire requested
native screen. Point framing and transition/return timing are unavailable in
that fallback. `fallback=error` declines the request and records a diagnostic
instead. Choose the policy deliberately for a scene that depends on a moving view.

Smooth transitions currently follow a connected vertical screen range. Between
disconnected rooms the provider cuts, even if their numeric IDs are adjacent.
It doesn't pan through unrelated screen numbers or invent a route through side
links. A more elaborate tour can use several explicit shots in a tree.

## Ownership and interaction

`active`, `position`, `duration` and `returnDuration` accept graph bindings and
effects. Prepared screen, transition mode, fallback and priority are structural.
Higher `priority` wins; the most recently activated request wins a tie. Repeated
updates to an active request don't steal ownership. Read `<id>.selected` to
confirm that the request is currently selected.

Replay camera sessions take precedence. A native ending releases MME's active
camera requests so its own framing can run. Disabling the scene, restarting,
restoring or unloading releases transient requests; active scene state can
request its view again after restoration. There is no body displacement to undo.

A camera show doesn't make a remote sound zone occupied, trigger the remote NPC,
change the player's `screenchanged` observation, or move collision queries to the
displayed screen. Native drawing uses a thread-local screen scope; updates and
save workers retain the real screen.

## Coordinate a reveal

Use one event to set a persistent/attempt variable and activate a finite camera
effect. Bind the distant artwork to that variable. The artwork then has its
correct state before the camera arrives, and remains correct when the player
later reaches that screen. [Remote changes](../scenes/remote-scenes.md) contains
the full pattern. Put sequencing in an ordinary tree when the camera must wait
for a conversation, sound or animation to finish.
