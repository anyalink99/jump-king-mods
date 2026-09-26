# Native map workflows

MME extends a normal Jump King level. The browser editor is optional. Keep the
native collision atlas, screen folders, props, audio, ending settings and credits
in their existing locations. Add only the files for features you need.

| Task | Native authoring path / MME extension |
| --- | --- |
| Platforms, water physics, snow, ice, slopes | Existing collision atlas and block palette; MME does not replace them |
| Background, midground, foreground, scrolling | Existing `screens/` files; MME Prop/Node layers add independently animated content |
| Hide/reveal artwork by contact | Existing `props/hidden_walls/hidden_wallN.xml`; observe its result with a Rule |
| Change several visual settings together | An Effect with Set entries, activated by a Rule or Region |
| Require actual platform support | Region `test="standing"`, optionally referencing an Anchor |
| Screen ambience and its fade | Existing `audio/background/data/values.xml` and compiled native audio |
| One-shot contact sound | Rule `sound`, referencing native `audio.music.event_music` |
| Babe position, ending type, rewards, credits | Existing native ending/settings and credits authoring |
| Replace an ending actor or controller tree | [ending/custom_*.xml](endings.md) |
| More screens and integer side links | [map.xml metadata](large-maps.md), alongside the real enlarged collision atlas |

## Hidden Kingdom uses Hidden Walls

The installed game's `hidden_wall80.xml` and `hidden_wall82.xml` describe the
red-mushroom buttons with RaymanData hitboxes and `block_appear` /
`block_disappear` sounds. RaymanWallEntity tests body-hitbox intersection and
fades its overlay over 0.2 seconds. This is not a special collision block and
does not require grounded state. MME observes that exact native test; it does
not approximate or duplicate the rectangles.

In your own native hidden-wall file, choose a stable `texture_name`. Rules can
then react to `hiddenwallenter:TEXTURE_NAME` and `hiddenwallexit:TEXTURE_NAME`.
For example, merge this Rules section into a scene that declares the referenced
effects and already has a native wall named `mushroom-cover`:

```xml-fragment
<Rules>
  <Rule id="reveal" event="hiddenwallenter:mushroom-cover"
        effect="reveal-room" owner="room" />
  <Rule id="conceal" event="hiddenwallexit:mushroom-cover"
        effect="conceal-room" owner="room" />
</Rules>
```

The native wall still owns its texture, fade and sounds. Do not repeat the same
sound on the Rule unless two playbacks are intentional. Names are exact and
case-sensitive. Missing native wall names reject resource preparation. MME
installs no Hidden Walls hook for scenes without these events. Restart starts
fresh native wall instances and fresh scene rule state.

Reference: [Nexile Hidden Walls authoring](https://teamnexile.github.io/jk-workshop-docs/level-making/props/hidden-walls/).

## A physical platform switches the room

This complete scene assumes your existing collision map has a solid ledge at
X=120..199, Y=280..287 on screen one. The Anchor documents it; it does not create
collision. Stand on the ledge to fade down the room. Jump away to release the
effect and return to the authored baseline.

```xml
<MegaMapping version="1">
  <Options advancedLighting="true" ambientIntensity="0.8" />
  <Anchors><Anchor id="ledge" screen="1" x="120" y="280" width="80" height="8" kind="solid" /></Anchors>
  <Effects>
    <Effect id="dim-room" fadeIn="0.2"><Set target="options" property="ambientIntensity" value="0.3" /></Effect>
  </Effects>
  <Regions><Region id="button" anchor="ledge" test="standing" enter="dim-room" lifetime="inside" /></Regions>
</MegaMapping>
```

Use `grounded="true"` with a normal rectangle when you want native
ScreenEventManager-style “enter this area, but wait until landing” behavior.
Use `standing` when a particular support matters. Use `hitbox` to match the
overlap semantics of Hidden Walls. These are deliberately different tests.

## Coordinated states without another state-machine format

An Effect is already a named preset. Give mutually exclusive presets the same
`group`, and their triggering Rules/Regions the same `owner`. A replacement
validates all target IDs and values before removing the previous preset. Each
Set entry uses the same property name exposed by the inspector and public API.

Use one preset for light intensity, fog opacity, water ripple, rain wind, plant
sway and shadow opacity. Numeric `fadeIn`/`fadeOut` interpolate these properties;
flags, colors and asset IDs switch discretely. For a background dissolve, place
both background variants on their intended layers and animate their opacities
together. This keeps standard layers and uses already prepared assets. MME does
not silently replace a native screen background with a different image.

Group replacement fades a new numeric layer from the authored/lower-priority
baseline, not a captured screenshot of the previous state. Put every coordinated
property in each preset when that state must fully define the room. A region's
`lifetime="inside"` cancels immediately on exit; use an explicit exit preset
with fadeIn when a gradual return is wanted. See [ownership and overlap](behaviors.md#effects-and-overlap).

## Native audio

Keep continuous screen ambience in the native ambience files. This preserves its
normal screen transitions, volume preference and fade behavior. For a Rule cue,
compile the sound to XNB under `audio/music/event_music/KEY.xnb`, then use
`sound="KEY"`. Native built-in event SFX loaded into the same dictionary also
work. MME invokes the same loaded object as PlayEventSFX/Hidden Walls. It does
not load audio when a contact occurs, own that native object, change the master
volume, or introduce a separate music bus. Restarting a currently playing cue
follows the native JKSound.Play behavior. Cooldown/once/flags prevent unwanted
repeated cues. The native category determines whether music or SFX preferences
apply; the Rule does not reclassify the sound.

## Validation, strict errors and installation

1. Author native files and optional MME scene/ending files by hand.
2. Run `SceneCacheCompiler --validate LEVEL_ROOT` to check scene and ending XML.
   Native sound/wall resources bind during BeforeAttempt, after game content loads.
3. Build the cache with `SceneCacheCompiler LEVEL_ROOT OUTPUT.mmgfx`, or deliberately
   ship only scene source. A map build pipeline can invoke this compiler.
4. Start a new attempt to test preparation; use the inspector for scene-only reload.
   Ending edits require a new attempt because the game owns existing actor trees.

An absent optional file means no override. A present invalid file is an error,
not permission to substitute vanilla behavior or compile another representation.
A stale/corrupt scene.mmgfx must be rebuilt or deliberately removed by the author.
Activation requires successful Runtime preparation; it never performs late disk
loading as a rescue path. Failed inspector reload is an explicitly rejected
transaction: the current scene stays intact and the error is reported.

Only needed systems do work: ordinary off-screen props have no per-frame pose
evaluation; water/puddle simulation runs for the current screen; zero-opacity
fog/rain/emitters skip drawing. Uniform ambient dimming needs no world copy or
light map. Advanced lighting and reflections still cost work when requested.
Authored texture variants remain preloaded so state changes never cause file IO;
there is no implicit texture streaming. The 512 MiB base-texture budget excludes
native game resources, masks and reload overlap. See [budgets](large-maps.md).

All original features and version-1 scene authoring remain available. Strict
cache errors are an intentional change: rebuild caches when upgrading the
compiler. Do not install two providers of the same custom-ending override.

[Handbook](authoring.md) · [Behavior reference](behaviors.md) · [Troubleshooting](troubleshooting.md)
