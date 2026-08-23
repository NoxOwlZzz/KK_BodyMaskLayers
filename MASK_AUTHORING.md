# Mask authoring guide

This guide describes the native categorical and continuous PNG formats. A mask is state data, not a diffuse texture.

## Canvas requirements

- PNG with an IHDR header.
- Square canvas.
- Power-of-two dimensions.
- 8 bits per channel.
- PNG color type 2, 3, 4 or 6.
- Fixed internal dimension range: 1×1 through 4096×4096.
- Fixed internal embedded-PNG cap: 32 MiB per slot.
- Every slot uses the target character's **base-body UV layout**, never the garment mesh UV. This is also true for Bottom, Bra, Shorts, Gloves, Pantyhose, Socks and both shoe slots: the PNG describes which pixels of the body disappear, not pixels of the clothes.

The dimension and byte caps are format-safety guards, not user-configurable import settings.

The plugin preserves the exact source PNG bytes. Categorical masks compile to bitsets and resize with nearest-neighbor. Continuous masks compile to one 8-bit hide-coverage plane per required state and resize those planes with bilinear interpolation after decoding.

## Palette and state behavior

| Author color | RGB | Full (0) | Partial (1/2) | Off (3) | Meaning |
|---|---:|---:|---:|---:|---|
| Yellow | `255,255,0` / `#FFFF00` | visible | visible | visible | Never hide |
| Green | `0,255,0` / `#00FF00` | hidden | visible | visible | Hide only while fully worn |
| Black | `0,0,0` / `#000000` | hidden | hidden | visible | Hide until the item is off |
| Red | `255,0,0` / `#FF0000` | hidden | hidden | visible | Same visibility as black; counted separately |

The clothing state belongs to the selected slot. A Bottom mask follows Bottom state even if Top is Off. Multiple active sources combine by maximum hide coverage, which is equivalent to Boolean OR at exact 0/255 endpoints.

## Continuous R/G authoring

In `Gradient Handling = Auto`, exact canonical colors keep the table above. Other native pixels are accepted as continuous only when B is zero and `R <= G`: Full hide coverage is `255 - R`, Partial states 1 and 2 use `255 - G`, and Off contributes nothing. Source alpha remains diagnostic metadata.

This supports antialiased edges and intentional black-to-green, green-to-yellow, or black-to-yellow ramps without categorical snapping. A native pixel with B data or non-monotonic `R > G` is ambiguous in `Auto` and follows `UnknownColorPolicy`. `PreserveContinuous` accepts explicit non-monotonic R/G state coverage for non-canonical values; the four canonical colors and `#4CFF00` always retain their categorical meaning. `StrictCategorical` restores the 0.1.x palette-only decoder and bounded compatibility normalization.

## Why blue is unsupported

Do not use blue as a fifth category. Pure blue has R=G=0, but the B channel can contain unrelated packed information in shader ecosystems. There is no confirmed vanilla authoring meaning for blue. `UnknownColorPolicy=RejectMask` therefore rejects it by default and diagnostics report blue pixel counts.

## Alpha channel

Source alpha does not control body hiding. Masks without alpha exist in the installed ecosystem. Use fully opaque palette colors for clarity. Unexpected alpha values may be reported, but they are not a fifth semantic category.

## Recommended categorical workflow

1. Start from the correct Koikatsu base-body UV/template for the target body/uncensor. Never paint against the clothing mesh UV.
2. Work at 512×512 or the same power-of-two resolution as the upstream mask.
3. Disable antialiasing and feathering when authoring a categorical/bitset mask.
4. Fill every pixel with one of the four canonical colors.
5. Use yellow as the neutral background.
6. Paint green where the body should be hidden only in Full.
7. Paint black or red where hiding should continue through Partial.
8. Export PNG in RGB or RGBA, 8-bit per channel, with mipmap generation disabled.
9. Load it in Maker, inspect the reported dimensions/hash/statistics, and test every state available for that garment.
10. Export it again from the plugin and compare SHA-256 if exact round-trip matters.

## Edges and antialiasing

Hard palette edges remain the smallest and fastest representation. Default `Auto` preserves safe intermediate R/G values as 8-bit coverage. The exported-green alias `#4CFF00` remains categorical in every mode; the older `Threshold` tolerance and bounded nearby-palette normalization apply only in `StrictCategorical`.

If an existing asset has antialiasing:

- use `Auto` and keep the transition in compatible R/G space with B=0; or
- quantize to the four-color palette and use `StrictCategorical` when hard edges are intended;
- inspect continuous, edge, ambiguous, packed/B-data and unexpected-alpha diagnostics.

## Choosing black versus red

In the verified Body Alpha shader formula, black and red produce the same Full/Partial visibility because G is zero for both and R cannot rescue the Full result. The plugin keeps their counts separate so future research or asset conventions remain observable. For new masks, prefer black unless an established asset workflow requires red.

## Slot and item binding

Import into the slot that owns the garment. Binding is always based on both the slot and the currently equipped item. Replacing that item leaves the PNG stored but inactive. Use **Bind mask to current item** only after confirming the replacement garment uses a compatible body UV/cutout.

## Maker load, save and export workflow

1. Equip the intended item, open its stock clothing tab, and scroll to **Body alpha mask**.
2. Select **Load new mask texture**. A valid image is embedded immediately in the current outfit record and bound to the equipped item.
3. Confirm the preview and concise **Status**. Use **Bind mask to current item** after an intentional replacement.
4. Cycle the garment through Full, every available Partial/Half level, and Off. For continuous assets, inspect edge intensity as well as categorical regions.
5. Save the character card and/or coordinate normally in Maker. Extended Save writes every slot's original PNG bytes and metadata into that outfit; the external source path is no longer needed. Reload a duplicate card and coordinate to verify the round-trip before distribution.
6. **Export mask texture** writes the exact embedded bytes. **Clear mask texture** removes only that slot record; **Enable loaded mask** temporarily disables/enables it without discarding the PNG.

If the mask hides too much body, move coverage toward green (Full-only), yellow (neutral), or lower continuous hide intensity. Source alpha is not the remedy. Reload and retest all states and overlapping layers.

## Resolution strategy

Larger images increase card/coordinate size and dirty-rebuild cost. Start at 512 unless fine UV boundaries require more. Keep categorical resizing nearest-neighbor; resize decoded continuous coverage with monotonic interpolation. Test several body shapes and the intended body/uncensor.

## Preflight checklist

- [ ] Square, power-of-two, 8-bit PNG.
- [ ] Resolution inside the fixed internal 1×1–4096×4096 range.
- [ ] Embedded PNG no larger than the fixed internal 32 MiB cap.
- [ ] Categorical: only yellow, green, black and optionally red; or continuous: intentional R/G with B=0.
- [ ] No unknown packed/blue data.
- [ ] Edge and alpha diagnostics match the intended authoring mode.
- [ ] Base-body UV used (never the garment UV), with mipmaps disabled.
- [ ] Several representative body shapes/sizes inspected.
- [ ] Correct garment and slot selected before binding.
- [ ] Full, every available Partial state and Off inspected in-game.
- [ ] Overlap with other active layers inspected.
- [ ] Card and coordinate round-trip tested before distribution.

The last three checks require an actual game run; they were not performed as part of the static development pass.
