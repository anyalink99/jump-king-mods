# Animation and counters

[Handbook](../index.md) / [Scene authoring](index.md)

Use an atlas for a repeating animation or an authored set of states. Declare its grid once, then let automatic playback or a graph choose a frame. Start with the sample `mme-assets` folder from [the minimal scene](../../examples/minimal/mapping_settings.xml).

## Play an atlas

This complete example plays the ten sample digit cells as a simple animation. It demonstrates the file format; replace the artwork with your own sequence for a real animated prop.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets>
    <Asset id="sequence" path="mme-assets/digits.png" columns="10" rows="1" frames="10" fps="8" />
  </Assets>
  <Components>
    <Component id="pose" kind="transform"><Value name="position" value="240,180" /></Component>
    <Component id="image" kind="sprite">
      <Value name="transform" value="pose" /><Value name="asset" value="sequence" />
      <Value name="playback" value="pingpong" /><Value name="clock" value="presentation" />
    </Component>
  </Components>
</MappingSettings>
```

Atlas dimensions must divide exactly by columns and rows. Frames occupy cells in row order, starting at zero. `frames` can limit playback to the used cells; `fps` controls automatic playback. Keep padding inside each cell if it belongs to the drawing. Extra space outside a divisible grid is an error.

Playback is loop, once, pingpong or manual. Automatic playback derives from the selected scene clock and reconstructs after restore. Gameplay stops at completion; Presentation can continue, but freezes in pause by default. Select manual when a graph binds `frame`.

## Select frames from an observation

The `atlas-display` recipe remaps a signal or variable from minimum/maximum to frame indices, clamps it, then rounds. Supply `asset`, `source`, `minimum`, `maximum` and the actual number of frames. Its default source is charge.heldFrames and its default range is 1вЂ“35; don't keep those defaults for an unrelated display.

For a direct graph, convert an Integer to Number, calculate an index, and declare `units=frame` at the conversion. `floor` alone doesn't change units. Binding frame doesn't require any clock-specific instrument implementation.

## Build rolling decimal digits

This scene counts jumps and shows two decimal places. It uses the ten-cell sample digits atlas, ordered 0 through 9:

```xml mapping_settings.xml
<MappingSettings version="1" saveId="handbook-rolling-count">
  <Assets><Asset id="digits" path="mme-assets/digits.png" columns="10" frames="10" /></Assets>
  <Variables><Variable id="jumps" kind="Integer" value="0" scope="Save" /></Variables>
  <Instances>
    <Instance id="ones" recipe="atlas-digit">
      <Arg name="asset" value="digits" /><Arg name="source" value="variable:jumps" />
      <Arg name="position" value="240,120" /><Arg name="roll" value="0.24" />
    </Instance>
    <Instance id="tens" recipe="atlas-digit">
      <Arg name="asset" value="digits" /><Arg name="source" value="variable:jumps" />
      <Arg name="position" value="228,120" /><Arg name="digit" value="1" />
    </Instance>
  </Instances>
  <Effects>
    <Effect id="grow" duration="0.3" fadeOut="0.3" clock="Presentation">
      <Set target="ones.transform/scale" value="1.12,1.12" />
      <Set target="tens.transform/scale" value="1.12,1.12" />
    </Effect>
  </Effects>
  <Rules>
    <Rule id="count" event="jump"><Action op="increment" target="jumps" value="1" /></Rule>
    <Rule id="feedback" event="variable:jumps"><Action op="effect" target="grow" /></Rule>
  </Rules>
</MappingSettings>
```

`digit=0` is ones, 1 is tens, 2 is hundreds. The recipe takes the absolute numeric value, divides by 10 to the chosen power, floors and wraps by ten. It doesn't add a minus sign, pad a label or choose spacing for you. Position each digit for your own cell width. The example grows each digit around its own transform; for a single group pulse, parent the images under a centered group transform and animate that group.

Transitions are none, slide and crossfade. `frameTransition` is seconds. Slide clips old/new drawing inside the same cell; crossfade intentionally blends colors. Use a screen-space mask for a larger fixed window around a moving/scaling digit group. Keep artwork and final geometry on the pixel grid for crisp digits.

Snapshots restore scene state without replaying the increment event. Saved totals and animation clocks serve different purposes; never reconstruct the count from the number of visual rolls. [Charge instruments](../observations/charge.md) use the same frame-selection mechanism with another source.
