# Vector assets

[Handbook](../index.md) / [Scene authoring](index.md)

MME can rasterize a small vector definition into a prepared texture. This is useful for reusable shapes, cutouts and generated art. The resulting asset participates in the same sprite, mesh, material and mask system as other artwork. It doesn't create animated path entities during play.

## A complete vector-backed sprite

These two files draw a small stepped diamond. The vector file lives under fragments here only to keep the example self-contained; any safe map-relative asset path works.

<!-- mme-example:start -->
```xml mapping_settings.xml
<MappingSettings version="1">
  <Assets><Asset id="diamond" kind="vector" path="fragments/diamond.xml" /></Assets>
  <Components>
    <Component id="pose" kind="transform"><Value name="position" value="220,160" /></Component>
    <Component id="diamond" kind="sprite"><Value name="transform" value="pose" /><Value name="asset" value="diamond" /></Component>
  </Components>
</MappingSettings>
```
```xml fragments/diamond.xml
<VectorAsset id="diamond" width="32" height="32" pixelSnap="true" supersample="1">
  <Shape type="path" data="M 16 2 L 30 16 L 16 30 L 2 16 Z" fill="#88BBDD" />
</VectorAsset>
```
<!-- mme-example:end -->

## Asset fields

Width/height default to 64 and may be 1вЂ“2048. Supersample defaults to 2 and allows 1вЂ“4. Vector pixelSnap selects rasterization style: true disables antialiasing and uses one sample; false permits smooth rasterization. It is separate from the transform's final geometry policy. The vector format's default is false, so set it true explicitly for crisp generated art. Transform snapping can't remove antialiasing already baked into an asset.

clipPath uses the path syntax below; clipMode is none, intersect or exclude. `edgeBleed` expands filled path edges to cover seams between neighboring traced polygons. `alphaCutoff` turns rasterized alpha into opaque/transparent pixels at a chosen threshold. `source` is descriptive source metadata; the renderer draws the Shapes in this file. These options are preparation-time data, not bindable component properties.

## Shapes and paths

Shapes draw in file order. Type is path, rect, ellipse or line. Rect/ellipse use x, y, width, height. A line starts at x,y and takes its endpoint from data="x2,y2". Paths support M (move), L (line), C (cubic), Q (quadratic) and Z (close), with lowercase relative forms. This is a limited path vocabulary, not a general SVG loader.

Fill defaults to white; an empty fill leaves it unfilled. Stroke and strokeWidth add an outline. Opacity scales alpha. Fill2 plus gradient selects a second color: none gives a solid fill, radial gives a radial gradient, and linear uses angle in degrees. Use simple closed paths and inspect the rasterized result at its actual size.

For repeated patterns or animation, rasterize the art once and animate its transform, UVs or atlas frame. Vector preparation has dimension and memory limits, and expensive rasterization belongs before activation. It isn't a shortcut around texture budgets.
