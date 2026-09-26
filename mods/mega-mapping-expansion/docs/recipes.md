# Small scene recipes

[Handbook](authoring.md) · [Parameter reference](parameter-reference.md) · [Troubleshooting](troubleshooting.md)

Each XML block is a complete independent `scene.xml` with inline vector assets.
It needs an existing playable map plus JK Runtime and Mega Mapping Expansion,
but no PNGs or external vector files. These intentionally small teaching scenes
explain effect setup; they are not replacements for the showcase's detailed art.
When combining recipes, merge collections under one root and keep IDs unique.
Do not append a second complete document or overwrite your main scene to try one.

## Fixed lamp with breathing glass

The post does not move. Only the glass's RGB gain changes; its opacity stays one.
The separate Light illuminates the world and the King can block it. Bring the
King near the source at (126,186) to inspect occlusion. Small flicker is independent
of the glass Track: they are not automatically phase-locked.

```xml
<MegaMapping version="1">
  <Options advancedLighting="true" ambientLight="#9DAFCC" ambientIntensity="0.45"
           playerRimColor="#9BBEDB" playerRimOpacity="0.3" playerRimPixels="1" />
  <Materials><Material id="warm-glass" tint="#FFD799" opacity="1" /></Materials>
  <Templates>
    <Template id="breathing" duration="3.4">
      <Track property="brightness" keys="0:1;0.5:1.5;1:1" easing="sine" />
    </Template>
  </Templates>
  <VectorAssets>
    <Asset id="post" width="20" height="98" pixelSnap="true">
      <Shape type="rect" x="8" y="22" width="4" height="76" fill="#263744" />
      <Shape type="path" data="M0 6 L10 0 L20 6 Z" fill="#344957" />
      <Shape type="rect" x="2" y="6" width="16" height="20" fill="#182431" />
    </Asset>
    <Asset id="glass" width="10" height="12" pixelSnap="true">
      <Shape type="rect" x="0" y="0" width="10" height="12" fill="#BB9255" />
      <Shape type="rect" x="3" y="2" width="4" height="8" fill="#E5C786" />
    </Asset>
  </VectorAssets>
  <Nodes>
    <Node id="lamp-post" asset="post" x="116" y="170" originX="0" originY="0"
          layer="world" z="10" rimOpacity="0.2" rimPixels="1" />
    <Node id="lamp-glass" asset="glass" x="126" y="186" layer="world" z="11"
          material="warm-glass" template="breathing" />
  </Nodes>
  <Lights>
    <Light id="lamp-light" x="126" y="186" radius="95" color="#FFD197"
           intensity="1.2" falloff="2.2" flicker="0.02" occludeKing="true" />
  </Lights>
</MegaMapping>
```

Tune ambient intensity first, then light intensity and falloff. Increase glass
brightness for a brighter visible bulb; increasing Light radius alone does not
animate or replace the glass artwork. Do not add opacity pulsing to a solid post.

## Rooted foliage in front of a drifting cloud

Sky, cloud and tree are distinct assets. Both cloud and tree use background but
different Z values, so cloud pixels cannot paint over opaque tree pixels. All
depth values remain one: no misalignment from player-relative parallax. The tree
origin stays at its root; use overscan in production art to hide cut branch ends.
The leaf silhouette here is deliberately simple so the layer relationship is clear.

```xml
<MegaMapping version="1">
  <VectorAssets>
    <Asset id="sky" width="480" height="360" pixelSnap="true">
      <Shape type="rect" x="0" y="0" width="480" height="360" fill="#17334D" />
    </Asset>
    <Asset id="cloud" width="150" height="40" supersample="2">
      <Shape type="ellipse" x="2" y="14" width="144" height="20"
             fill="#8EBAC855" fill2="#8EBAC800" gradient="radial" />
      <Shape type="ellipse" x="35" y="3" width="75" height="32"
             fill="#8EBAC844" fill2="#8EBAC800" gradient="radial" />
    </Asset>
    <Asset id="tree" width="96" height="220" pixelSnap="true">
      <Shape type="path" data="M44 220 L48 92 L30 62 L34 54 L53 78 L68 22 L74 24 L60 100 L58 220 Z" fill="#0A1A2C" />
      <Shape type="path" data="M4 64 L18 35 L34 42 L47 8 L64 18 L72 0 L90 35 L80 68 L94 94 L62 108 L50 92 L22 112 L0 88 Z" fill="#0D2339" />
    </Asset>
  </VectorAssets>
  <Nodes>
    <Node id="sky-back" asset="sky" x="0" y="0" originX="0" originY="0" layer="background" z="-100" />
    <Node id="cloud-drift" asset="cloud" x="195" y="70" layer="background" z="-80"
          sampling="linear" motion="orbit" amplitudeX="20" amplitudeY="2" duration="70" />
    <Node id="rooted-tree" asset="tree" x="225" y="270" originX="52" originY="220"
          layer="background" z="-50" wind="true" windHeight="220" windStrength="0.7" duration="5.6" />
  </Nodes>
</MegaMapping>
```

An opaque native midground can cover this recipe. Use an appropriately transparent
native midground in your map; changing mod Z cannot move background past it.
For a near interactive shrub, use a separate world Node with `reactRadius` and
`reactStrength`, and keep the pivot rooted. That reaction is a horizontal spring,
not collision against every leaf. Never use alpha cutoff on the cloud.

## Fast-winged moth on a smooth closed path

The central body remains solid. The left and right wing regions deform at 24 Hz
while the flight path completes in six seconds. A closed spline removes the hard
corners of straight path segments, though speed still varies with segment length.
The body is drawn vertically; orientationOffset corrects its forward direction
relative to the path tangent. Adjust that correction for your own art.

```xml
<MegaMapping version="1">
  <VectorAssets>
    <Asset id="moth" width="24" height="12" pixelSnap="true">
      <Shape type="path" data="M0 0 L10 4 L10 10 L2 8 Z" fill="#D9CBAB" />
      <Shape type="path" data="M14 4 L24 0 L22 8 L14 10 Z" fill="#C4B798" />
      <Shape type="rect" x="10" y="4" width="4" height="7" fill="#7B644D" />
    </Asset>
  </VectorAssets>
  <Nodes>
    <Node id="moth-flight" asset="moth" x="220" y="150" layer="foreground"
          trigger="screen-enter" motion="path" path="-32,0;-8,-24;34,-8;25,22;-18,20"
          pathInterpolation="spline" closedPath="true" orientToPath="true" orientationOffset="90"
          duration="6" flutterHz="24" flutterAmount="0.7"
          flutterBodyStart="10" flutterBodyEnd="14" opacity="1" />
  </Nodes>
</MegaMapping>
```

Use the preview to inspect the path seam and wing/body joints. Flutter is sampled
at the game's frame cadence: arbitrarily high frequencies can alias or appear
static. Scene entry resets flight age, not the global wing phase.

## Watching eyes with separate faint halos behind mist

Each eye has its own halo and pupil; they are not one large glow spanning the pair.
Pupils track the King within small bounded offsets. Near fog draws later than both
eyes. The glow is authored translucent geometry, not automatic fog scattering.

```xml
<MegaMapping version="1">
  <VectorAssets>
    <Asset id="halo" width="24" height="16" supersample="2">
      <Shape type="ellipse" x="0" y="0" width="24" height="16"
             fill="#B31B2528" fill2="#B31B2500" gradient="radial" />
    </Asset>
    <Asset id="eye" width="9" height="5" pixelSnap="true">
      <Shape type="path" data="M0 2 L3 0 L8 1 L9 3 L5 5 L1 4 Z" fill="#8E2733" />
    </Asset>
    <Asset id="pupil" width="3" height="3" pixelSnap="true">
      <Shape type="rect" x="0" y="0" width="3" height="3" fill="#EB5C51" />
    </Asset>
  </VectorAssets>
  <Nodes>
    <Node id="left-halo" asset="halo" x="232" y="125" layer="world" z="10" />
    <Node id="right-halo" asset="halo" x="251" y="125" layer="world" z="10" />
    <Node id="left-eye" asset="eye" x="232" y="125" layer="world" z="11" />
    <Node id="right-eye" asset="eye" x="251" y="125" layer="world" z="11" />
    <Node id="left-pupil" asset="pupil" x="232" y="125" layer="world" z="12"
          lookAtKing="true" gazeX="2" gazeY="1" gazeResponse="7" />
    <Node id="right-pupil" asset="pupil" x="251" y="125" layer="world" z="12"
          lookAtKing="true" gazeX="2" gazeY="1" gazeResponse="7" />
  </Nodes>
  <Fogs>
    <Fog id="near-mist" x="0" y="90" width="480" height="100" layer="foreground"
         color="#899DAA" opacity="0.16" speed="3" bands="7" />
  </Fogs>
</MegaMapping>
```

For blinking, add matching scaleY Tracks and durations to the socket and pupil
Nodes. A real local Light can illuminate nearby artwork, but has no receiver
mask; keep its radius and intensity small if you add one. Darken the backdrop
deliberately rather than expecting eye glow to define the whole exposure.

## Rain over a shallow rooftop puddle

This recipe assumes your native map has a solid platform with top Y=280,
X=90..390. Author that native platform separately. The vector top, metadata
Anchor and Puddle do not create it. The pink sign is a visible reflection source.
Feet should land on Y=280, with the puddle extending down into the painted top.

```xml
<MegaMapping version="1">
  <VectorAssets>
    <Asset id="roof" width="300" height="36" pixelSnap="true">
      <Shape type="rect" x="0" y="0" width="300" height="13" fill="#263847" />
      <Shape type="rect" x="0" y="13" width="300" height="23" fill="#101D2A" />
    </Asset>
    <Asset id="sign" width="32" height="55" pixelSnap="true">
      <Shape type="rect" x="0" y="0" width="32" height="55" fill="#302439" />
      <Shape type="rect" x="5" y="5" width="4" height="45" fill="#EA638F" />
      <Shape type="rect" x="15" y="12" width="12" height="3" fill="#E792B1" />
    </Asset>
  </VectorAssets>
  <Nodes>
    <Node id="roof-art" asset="roof" x="90" y="280" originX="0" originY="0" layer="world" />
    <Node id="neon-sign" asset="sign" x="240" y="224" layer="world" />
  </Nodes>
  <Anchors>
    <Anchor id="roof-guide" screen="1" x="90" y="280" width="300" height="36" kind="solid" />
  </Anchors>
  <Rains>
    <Rain id="far-rain" layer="background" count="60" speed="150" wind="-18" length="4" opacity="0.12" />
    <Rain id="near-rain" layer="foreground" count="100" speed="230" wind="-32" length="7" opacity="0.25" collide="true" />
  </Rains>
  <Puddles>
    <Puddle id="roof-puddle" planeY="280" outline="180,281;280,281;302,287;270,292;190,291;172,286"
            reflectionScaleY="0.25" perspective="0.006" opacity="0.6" ripple="0.6" rainRings="8" color="#B8CADB" />
  </Puddles>
</MegaMapping>
```

Move the King above the surface to see a compressed reflection. Rain clipping
uses the Anchor's upper edge, not the decorative art. The puddle's ambient rings
continue independently of rain streaks. Do not mark the entire roof-art Node as
an occluder: that would paint over this puddle. Split a dry fascia or foreground
rim into its own occluder only where needed. See Water in the reference for a
deeper lake with a native water volume and interactive wave field.
