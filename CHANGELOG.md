# Changelog

## Unreleased

- Removed local PDB paths from Release assemblies and added a PDB output guard.
- Added source-only repository exclusions and parameterized game-directory scripts.

## 0.1.2

- Replaced the generic empty-slot rejection with an explicit `Equip an item before loading a mask` state and disabled Load until that clothing slot has a bindable item.
- Reduced import work to one palette pass, reused identical decoded masks by SHA-256, and generated 150px previews from semantic data instead of decoding the full PNG a second time.
- Reduced runtime composition work with same-resolution fast paths, state/category skips, duplicate-mask coalescing, cached UI lookups, and retained output buffers across temporary Off or disabled states.

## 0.1.1

- Moved the Maker UI into the nine stock clothing tabs and matched the native KCOX-style layout with a 150x150 preview, vertical actions, separators, and persistent status feedback.
- Added the common exported-green alias and bounded compatibility normalization for nearby exported or antialiased palettes when no blue pixels are present and at most 12.5% of pixels fall outside the configured threshold.
- Disabled layer-only actions until a mask is loaded, protected file-dialog callbacks across Maker exit, and added clear rejection and error logging.

## 0.1.0

- Added nine slot-specific layers with independent clothing-state behavior and OR composition.
- Preserved the vanilla or external base mask, including blue and alpha channels.
- Added Maker controls for loading, clearing, exporting, binding, and enabling layers.
- Added versioned outfit, card, and coordinate persistence for original PNG bytes.
- Added optional Sideloader identity and partial Coordinate Load Option integration.
- Added cooperative ordering with ChaAlphaMask.
- Added diagnostics and .NET Framework 3.5 pure-logic tests.
