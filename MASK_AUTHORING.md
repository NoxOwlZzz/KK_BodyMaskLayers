# Mask authoring guide

A body alpha mask is state data mapped to the character's base-body UV layout. It is not a diffuse texture and must not use the garment mesh UV.

## Image requirements

- PNG with a valid IHDR header.
- Square, power-of-two canvas.
- 8 bits per channel.
- PNG color type 2, 3, 4, or 6.
- Embedded PNG size no greater than the 32 MiB cap.

The plugin stores the exact PNG bytes. Categorical masks compile to bitsets and use nearest-neighbor resizing. Continuous masks compile to one hide-coverage plane per required clothing state and use bilinear interpolation after decoding.

## Palette and state behavior

| Author color | RGB | Full (0) | Partial (1/2) | Off (3) | Meaning |
|---|---:|---:|---:|---:|---|
| Yellow | `255,255,0` / `#FFFF00` | visible | visible | visible | Never hide |
| Green | `0,255,0` / `#00FF00` | hidden | visible | visible | Hide only while fully worn |
| Black | `0,0,0` / `#000000` | hidden | hidden | visible | Hide until the item is off |
| Red | `255,0,0` / `#FF0000` | hidden | hidden | visible | Same visibility as black |

The clothing state belongs to the selected slot. A Bottom mask follows Bottom even when Top is off. Multiple active masks combine by maximum hide coverage, equivalent to Boolean OR at exact coverage endpoints.

## Continuous R/G authoring

With `Gradient Handling = Auto`, the four palette colors keep the table above. Other pixels are continuous only when B is zero and `R <= G`: Full hide coverage is `255 - R`, Partial states use `255 - G`, and Off contributes nothing.

This supports antialiased edges and intentional ramps without categorical snapping. A pixel with B data or non-monotonic `R > G` follows `UnknownColorPolicy`. `PreserveContinuous` accepts explicit non-monotonic R/G state coverage for non-palette values. `StrictCategorical` accepts the defined categorical palette and its bounded tolerance handling.

## Blue and alpha

Do not use blue as an additional category. The B channel can contain unrelated packed information, so `UnknownColorPolicy=RejectMask` rejects it by default.

Source alpha does not control body hiding. Use opaque palette colors for clarity. Unexpected alpha values can appear in diagnostics but do not define another mask state.

## Categorical workflow

1. Start from the correct base-body UV/template for the intended body or uncensor.
2. Use a square power-of-two canvas consistent with that template.
3. Disable antialiasing and feathering for a categorical mask.
4. Fill every area with yellow, green, black, or red.
5. Use yellow as the neutral background.
6. Paint green where hiding should occur only in Full.
7. Paint black or red where hiding should continue through Partial.
8. Export RGB or RGBA PNG without mipmaps.
9. Load the image in Maker and inspect every state available to the garment.
10. Export it from the plugin when an exact byte-for-byte round trip must be verified.

For antialiased assets, keep transitions in compatible R/G space with B=0, or quantize to the four-color palette when hard edges are intended.

## Slot and item binding

Import into the slot that owns the garment. Binding always includes both the slot and the equipped item identity. Replacing that item leaves the stored PNG inactive. Use **Bind mask to current item** only after confirming the replacement garment uses compatible body coverage.

## Maker save and export workflow

1. Equip the item, open its stock clothing tab, and scroll to **Body alpha mask**.
2. Select **Load new mask texture**. The PNG is embedded in the current outfit and bound to the equipped item.
3. Confirm the preview and concise **Status**.
4. Inspect Full, each available Partial state, and Off.
5. Save the character card or coordinate normally.
6. Reload a copy and confirm that the mask and binding round-trip.

**Export mask texture** writes the exact embedded PNG. **Clear mask texture** removes that slot's record. **Enable loaded mask** changes contribution without discarding the stored image.

## Preflight checklist

- [ ] Square, power-of-two, 8-bit PNG within the 32 MiB cap.
- [ ] Base-body UV used rather than garment UV.
- [ ] Categorical palette or intentional continuous R/G coverage with B=0.
- [ ] Edge, blue-data, and alpha diagnostics match the intended mode.
- [ ] Correct garment and slot selected before binding.
- [ ] Full, each available Partial state, and Off inspected in-game.
- [ ] Overlap with other active clothing masks inspected.
- [ ] Card and coordinate round-trip verified.
