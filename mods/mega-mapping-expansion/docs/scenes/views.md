# Tint a screen, mirror scenery or hide the timer

[Handbook](../README.md) / [Scene authoring](../scenes/README.md)

A `view` component changes screen presentation. It does not change platform collision. Use one global view with `screen=0` and, when needed, one view per specific screen. A specific view takes precedence over the global view.

## Try a screen tint

This complete file hides the timer globally and gives screen 1 a mild blue tint:

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="globalView" kind="view">
      <Value name="screen" value="0" />
      <Value name="timer" value="hide" />
    </Component>
    <Component id="firstView" kind="view">
      <Value name="screen" value="1" />
      <Value name="tint" value="#88BBFF" />
      <Value name="tintAmount" value="0.2" />
      <Value name="timer" value="hide" />
    </Component>
  </Components>
</MappingSettings>
```

1. Load it in a working map. Screen 1 should have the chosen tint and no timer.
2. Set `tintAmount` to `0` and rebuild: the tint disappears.
3. Change timer to `show` to display it explicitly, or `inherit` to use the game's normal choice.

Screen-specific views use their own settings; don't assume omitted properties merge with a global view.

## Other settings

`ambient` sets the ambient color. `exposure` changes brightness. `mirror` accepts `none`, `horizontal`, `vertical` or `both`. A visual mirror does not mirror the King's collision or routes; test carefully before using it in a playable room.

`intro` replaces the simple native opening string. Put an escaped `&lt;newline&gt;` in the XML attribute for a native line break. For several pages or font/fade control, use [intro components](../game/results.md#replace-the-opening-legend-with-several-pages).

`animateWhilePaused` is false by default. Turning it on permits Presentation-clock visuals during pause but holds scene logic, camera and audio. See [clocks](clocks.md) before enabling it.
