# Mask authoring guide

This guide describes the categorical PNG format. It is intentionally strict: a mask is data, not a shaded image.

## Canvas requirements

- PNG with an IHDR header.
- Square canvas.
- Power-of-two dimensions.
- 8 bits per channel.
- PNG color type 2, 3, 4 or 6.
- Default accepted range: 128×128 through 2048×2048.
- Default embedded-file limit: 16 MiB per slot.
- Every slot uses the target character's **base-body UV layout**, never the garment mesh UV. This is also true for Bottom, Bra, Shorts, Gloves, Pantyhose, Socks and both shoe slots: the PNG describes which pixels of the body disappear, not pixels of the clothes.

The plugin preserves the exact source PNG bytes, but internally classifies pixels. Resizing during composition uses nearest-neighbor. Do not rely on smooth grayscale transitions.

## Palette and state behavior

| Author color | RGB | Full (0) | Partial (1/2) | Off (3) | Meaning |
|---|---:|---:|---:|---:|---|
| Yellow | `255,255,0` / `#FFFF00` | visible | visible | visible | Never hide |
| Green | `0,255,0` / `#00FF00` | hidden | visible | visible | Hide only while fully worn |
| Black | `0,0,0` / `#000000` | hidden | hidden | visible | Hide until the item is off |
| Red | `255,0,0` / `#FF0000` | hidden | hidden | visible | Same visibility as black; counted separately |

The clothing state belongs to the selected slot. A Bottom mask follows Bottom state even if Top is Off. Multiple active layers combine by OR: a body pixel remains hidden when any eligible layer asks to hide it.

## Why blue is unsupported

Do not use blue as a fifth category. Pure blue has R=G=0, but the B channel can contain unrelated packed information in shader ecosystems. There is no confirmed vanilla authoring meaning for blue. `UnknownColorPolicy=RejectMask` therefore rejects it by default and diagnostics report blue pixel counts.

## Alpha channel

Source alpha does not control body hiding. Masks without alpha exist in the installed ecosystem. Use fully opaque palette colors for clarity. Unexpected alpha values may be reported, but they are not a fifth semantic category.

## Recommended workflow

1. Start from the correct Koikatsu base-body UV/template for the target body/uncensor. Never paint against the clothing mesh UV.
2. Work at 512×512 or the same power-of-two resolution as the upstream mask.
3. Disable antialiasing, feathering, color management transforms and lossy export.
4. Fill every pixel with one of the four canonical colors.
5. Use yellow as the neutral background.
6. Paint green where the body should be hidden only in Full.
7. Paint black or red where hiding should continue through Partial.
8. Export PNG in RGB or RGBA, 8-bit per channel, with mipmap generation disabled.
9. Load it in Maker, inspect the reported dimensions/hash/statistics, and test every state available for that garment.
10. Export it again from the plugin and compare SHA-256 if exact round-trip matters.

## Edges and antialiasing

For deterministic results, use hard palette edges. The default `Threshold` classifier accepts small encoder deviations with `ColorTolerance=12`, and recognizes the common exported green `#4CFF00` as green. If a rejected Threshold mask contains no blue and at most 12.5% nearby unknown pixels, version 0.1.1 retries those exported/antialiased palette pixels with bounded nearest-category normalization. Larger or distant deviations remain rejected because they are more likely to be the wrong kind of image.

If an existing asset has antialiasing:

- quantize it to the four-color palette before import; or
- increase tolerance cautiously and inspect diagnostics;
- do not use `NearestCategory` merely to silence unknown-color warnings without visual review.

## Choosing black versus red

In the verified Body Alpha shader formula, black and red produce the same Full/Partial visibility because G is zero for both and R cannot rescue the Full result. The plugin keeps their counts separate so future research or asset conventions remain observable. For new masks, prefer black unless an established asset workflow requires red.

## Slot and item binding

Import into the slot that owns the garment. With default `MaskBindingMode=SlotAndItem`, the plugin binds the mask to the currently equipped item. Replacing that item leaves the PNG stored but inactive. Use **Bind to current item** only after confirming the new garment uses a compatible body UV/cutout.

`SlotOnly` intentionally follows any present item in the slot and can cause holes or clipping with incompatible garments. It is an advanced troubleshooting/authoring mode, not the safe default.

## Maker load, save and export workflow

1. Equip the intended item, open its stock clothing tab, and scroll to **Body alpha mask**.
2. Select **Load new mask texture**. A valid image is embedded immediately in the current outfit record and bound to the equipped item by default.
3. Confirm the preview and concise **Status**. Use **Bind mask to current item** after an intentional replacement.
4. Cycle the garment through Full, every available Partial/Half level, and Off. Black/red must hide in Full and Partial; green only in Full; yellow never contributes.
5. Save the character card and/or coordinate normally in Maker. Extended Save writes every slot's original PNG bytes and metadata into that outfit; the external source path is no longer needed. Reload a duplicate card and coordinate to verify the round-trip before distribution.
6. **Export mask texture** writes the exact embedded bytes. **Clear mask texture** removes only that slot record; **Enable loaded mask** temporarily disables/enables it without discarding the PNG.

If the mask hides too much body, return to the source image and replace excess black/red areas with green (when hiding is needed only in Full) or yellow (when that body area should never be hidden by this garment). Keep hard edges, make small changes around seams, then reload and retest all states and overlapping layers. Do not solve over-hiding by adding transparency, gradients, or garment-UV painting.

## Resolution strategy

Larger images increase card/coordinate size and dirty-rebuild cost. Start at 512 unless fine UV boundaries require more. Test 1024/2048 only where the visual difference is real. Do not upscale a categorical mask with bilinear/bicubic filters; use nearest-neighbor. Test the result on several body shapes/sizes and with the intended body/uncensor, because deformation and UV variants can expose seams even though the mask itself always addresses the base body.

## Preflight checklist

- [ ] Square, power-of-two, 8-bit PNG.
- [ ] Resolution inside configured limits.
- [ ] File size inside configured limit.
- [ ] Only yellow, green, black and optionally red.
- [ ] No blue pixels.
- [ ] No unintended transparent/antialiased edge colors.
- [ ] Base-body UV used (never the garment UV), with mipmaps disabled.
- [ ] Several representative body shapes/sizes inspected.
- [ ] Correct garment and slot selected before binding.
- [ ] Full, every available Partial state and Off inspected in-game.
- [ ] Overlap with other active layers inspected.
- [ ] Card and coordinate round-trip tested before distribution.

The last three checks require an actual game run; they were not performed as part of the static development pass.
