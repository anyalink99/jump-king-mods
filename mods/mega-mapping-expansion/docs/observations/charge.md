# Charge observations

[Handbook](../index.md) / [Game observations](index.md)

Charge is a reusable observation contract. It can drive a dial, material opacity, deformation, particle rate, a light, a rule condition or a C# consumer. No special clock or instrument object owns it.

## What the values mean

| Signal | Type / unit | Evidence |
|---|---|---|
| charge.active | Boolean | Native charging state |
| charge.nativeTimerSeconds | Number / seconds | Sampled native timer |
| charge.elapsedMilliseconds | Number / ms | Sampled charge duration |
| charge.heldFrames | Number / F | Estimated during charging; exact after known corrected release |
| charge.releasedFrames | Number / F | Exact corrected launch when available; otherwise Unavailable |

F is the game's launch-frame quantity. It isn't a uniform real-time sample grid. Frame pacing and Subframe Charge affect reachable fractions. Don't assume seventeen fractional positions, a 0.1 step or a fixed count of reachable values. During charge, use an estimate for presentation. Judge the completed launch from corrected releasedFrames, with a quality guard.

Pause stops charge advancement. Teleporting ends the current charging phase. Restart/restore follow the attempt's lifecycle; they don't invent a new launch event. `chargestarted`, `chargeended` and `jump` are different events. A cancelled charge isn't necessarily a jump.

## A charge-driven display

This complete example maps F1вЂ“F35 onto ten sample cells. The sample digits are only stand-in artwork; a real dial should have authored hand positions and marks.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets><Asset id="dial" path="mme-assets/digits.png" columns="10" frames="10" /></Assets>
  <Instances>
    <Instance id="meter" recipe="atlas-display">
      <Arg name="asset" value="dial" /><Arg name="source" value="charge.heldFrames" />
      <Arg name="minimum" value="1" /><Arg name="maximum" value="35" />
      <Arg name="frames" value="10" /><Arg name="position" value="240,120" />
    </Instance>
  </Instances>
</MappingSettings>
```

To show only while charging, read charge.active and bind it to the exported image's visible property. To preserve the last launch result, leave it visible. The recipe exposes transform and image ports so you can move it or apply feedback without changing its internals.

## Judge a corrected launch

This scene flashes a light if corrected release is within 0.001 F of 11.5177. The tolerance is an authored policy, not a claim about every possible input device. No signal means no assessment.

```xml mapping_settings.xml
<MappingSettings version="1">
  <Components>
    <Component id="pose" kind="transform"><Value name="position" value="240,120" /></Component>
    <Component id="lamp" kind="light">
      <Value name="transform" value="pose" /><Value name="intensity" value="0" />
      <Value name="color" value="#88DDAA" />
    </Component>
  </Components>
  <Graph>
    <Node id="released" op="input" source="charge.releasedFrames" quality="Exact" />
    <Node id="known" op="available" inputs="released" />
    <Node id="target" op="constant" value="11.5177" units="F" />
    <Node id="error" op="subtract" inputs="released;target" />
    <Node id="absoluteError" op="abs" inputs="error" units="F" />
    <Node id="tolerance" op="constant" value="0.001" units="F" />
    <Node id="near" op="less" inputs="absoluteError;tolerance" />
    <Node id="accepted" op="select" inputs="known;near;no" />
    <Node id="no" op="constant" kind="Boolean" value="false" />
  </Graph>
  <Effects>
    <Effect id="flash" duration="0.5" fadeOut="0.5" clock="Presentation">
      <Set target="lamp/intensity" value="0.08" />
    </Effect>
  </Effects>
  <Rules><Rule id="assess" event="jump" condition="accepted"><Action op="effect" target="flash" /></Rule></Rules>
</MappingSettings>
```

The select uses a known false branch when corrected evidence is absent. Merely comparing an unavailable value to zero would make the logic depend on evidence that doesn't exist. The same accepted condition could increment a precise-jump counter or emit a custom event.

## Split fine precision across instruments

For a readable three-dial display, use one dial for whole F and two for an explicitly chosen fractional scale. Decimal tenths/hundredths are easy to explain: subtract floor(F), multiply the fraction by ten or one hundred, floor and wrap by ten. This labels decimal bands; it doesn't claim each band is reachable.

If your scene instead follows 170 substeps per F, calculate `round(fraction * 170)`, wrap by 170, then split that integer into quotient-by-ten and remainder. The right dials need 17 and 10 states. Document that mapping in the map's source: those marks are substep positions, not the four decimal digits printed in F11.5177. Keep exact release assessment separate from display rounding.

A source can feed any compatible property. To drive light intensity, remap/clamp it to a modest brightness range. To drive deformation, map it to displacement in pixels. To drive an atlas, convert to frame units. This is the same [typed graph](../scenes/behaviors.md), not a charge-specific rendering API.
