# Reveal something on another screen

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

Use a two-screen working map for this example. An area on screen 1 reveals a panel on screen 2 and briefly shows it with the camera. Copy `mme-assets/panel.png` from lesson 1, paste the complete scene below into `mapping_settings.xml`, and [build and load](../getting-started/worldsmith.md).

Enter the area centered at `(100,200)` with size `120,120` on screen 1. You should see screen 2 temporarily, then return to normal tracking. The panel remains revealed for the attempt. A cut works without Smooth Camera; smooth travel needs the optional mod.

Variables and IDs are shared by the whole scene. A transform chooses where its image draws, so a screen-1 trigger can change screen-2 artwork while it is offscreen.

## Show a distant change

Use this complete example in a map with at least two screens. Copy the sample
`mme-assets/panel.png` from the minimal example. Enter the rectangle on screen 1
to reveal a panel on screen 2 and briefly show that screen with the camera.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Variables>
    <Variable id="opened" kind="Boolean" value="false" />
  </Variables>
  <Assets>
    <Asset id="panelArt" path="mme-assets/panel.png" />
  </Assets>
  <Components>
    <Component id="triggerPose" kind="transform">
      <Value name="position" value="100,200" />
    </Component>
    <Component id="trigger" kind="sensor">
      <Value name="transform" value="triggerPose" />
      <Value name="size" value="120,120" />
      <Value name="phase" value="scene" />
      <Value name="initial" value="fire" />
    </Component>
    <Component id="remotePose" kind="transform">
      <Value name="screen" value="2" />
      <Value name="position" value="220,160" />
    </Component>
    <Component id="panel" kind="sprite">
      <Value name="transform" value="remotePose" />
      <Value name="asset" value="panelArt" />
    </Component>
    <Component id="shot" kind="camera">
      <Value name="screen" value="2" />
      <Value name="transition" value="smooth" />
    </Component>
  </Components>
  <Graph>
    <Node id="opened" op="input" source="variable:opened" />
  </Graph>
  <Bindings>
    <Bind id="panel" source="opened" target="panel/visible" />
  </Bindings>
  <Effects>
    <Effect id="show" duration="2.5">
      <Set target="shot/active" value="true" />
    </Effect>
  </Effects>
  <Rules>
    <Rule id="open" event="trigger.enter" once="true">
      <Action op="set" target="opened" value="true" />
      <Action op="effect" target="show" />
    </Rule>
  </Rules>
</MappingSettings>
```

The scene variable changes before the following draw. The camera effect is
temporary; `opened` remains true for the attempt. To retain it across normal
attempts, give the scene a stable `saveId` and the variable `scope=Save`.
See [state and saves](state.md) for native reset and shared-world behavior.

This example reveals artwork. A solid door needs a separate supported collision
or link mechanic; a sprite becoming invisible doesn't remove native collision.
For a route that opens after an event, use a [transition scheme](../maps/teleports.md)
and emit its configured trigger from the same rule.

## Keep the meanings separate

An event's screen is where its source interaction happened. The player's screen
is where gameplay and collision run. A component's screen chooses its drawing
placement. A camera's screen chooses what is shown. These can all be different.

Changing a distant material, animation frame, light or variable needs no camera
movement. Conversely, a camera shot doesn't create occupancy in a remote sensor
or advance a remote NPC conversation. Native systems retain their own update
rules. Use native observations for native behavior and scene state for scene
reactions.

For a longer reveal, use a tree: start a camera effect, wait, emit an event that
changes the distant scene, wait again, then clear the tree's effects. Each step
uses existing [tree operations](behavior-trees.md). The same sequence can start
from a Hidden Wall, an NPC line, a pickup or an ordinary scene event.
