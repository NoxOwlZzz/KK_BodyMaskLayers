# Changelog

## 0.3.1 - 2026-09-05

- Restored skin visibility when Studio hides gloves, socks, shoes, or pantyhose using a clothing state that is not `3`.
- Applied the game's slot visibility rules consistently to native and imported masks, change detection, and diagnostics while retaining card and coordinate data.

## 0.3.0 - 2026-09-04

- Made compatible metadata loading, conversion, indexing, and coexistence automatic instead of separate configuration modes.
- Moved character mask processing to late-frame execution so clothing visibility changes are composed before rendering.
- Renamed the compatibility subsystem around external and imported data while preserving serialized provider IDs, fingerprints, and schema values.
- Reduced configuration and documentation to the settings and files required to build, maintain, and use the plugin.
- Preserved pending composition, source resolution, and automatic-import persistence after transient failures.
- Removed unused internal helpers and an unused TextMeshPro build reference.
- Updated release metadata and public documentation for version 0.3.0.

## 0.2.0 - 2026-08-22

- Added direct compatibility for declared Sideloader body-mask metadata and PNG/AssetBundle textures without requiring another DLL or a card/coordinate resave.
- Added continuous 8-bit state coverage, categorical bitset fast paths, gradient-aware decoding, binary nearest-neighbor and continuous bilinear resampling, and max-hide composition with upstream B/A preservation.
- Added the BML2 schema for interpretation/provenance/fingerprint metadata while retaining BML1 reads and original PNG bytes.
- Added an incremental session manifest catalog, generation negative cache, bounded compiled-mask LRU, deterministic fingerprints and native/external duplicate suppression.
- Added silent automatic conversion of resolved external masks into portable native BML2 layers, with no source-specific Maker UI or prompts and no overwrite of existing native layers.
- Added numeric diagnostic counters, coalesced per-frame scheduling and unchanged-output upload suppression.
- Added pure tests for a pinned BML1 migration fixture, schema corruption, a 2,048-manifest cold/warm catalog, continuous gradients/antialiasing, 512/1024 resampling, mixed overlap, bounded caches, warm state-buffer reuse and a synthetic 600-tick idle contract.
- Fixed native and converted mask persistence across card, outfit and coordinate save/reload paths.
- Fixed binding to slot-and-item identity, removed the unsafe slot-only mode and retained explicit rebinding for deliberate garment replacements.
- Removed configurable PNG size controls while retaining the fixed 32 MiB format cap.
- Split persistence, native decode, clothing runtime, composition, diagnostics and external compatibility into cohesive owned components.
- Configured Release assemblies to omit debug symbols.
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
