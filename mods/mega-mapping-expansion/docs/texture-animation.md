# Texture animation

[Parameter reference](parameter-reference.md#texture)

Export animation frames into a uniform PNG grid. Declare the grid's columns and
rows on the scene Texture, and reference that texture from a Prop. Each page
must have identical dimensions, exactly divisible by the grid.

The Texture path is page zero. Add Page children in playback order for additional
pages, up to sixteen pages total. Frames advance left to right, then top to bottom,
then onto the next page. Set frames when the final page contains unused cells.

Set fps to the playback rate; zero holds the first frame. Playback wraps without
interpolation and follows the object's age. This is separate from the object's
motion loop. Keep the source animation outside the distributed map if only the
PNG pages are needed at runtime.

All pages load together. Paging limits individual texture dimensions; it does not
reduce total decoded memory. Check the [scene budget](large-maps.md#resource-budget)
and allow additional memory for native content and rendering.
