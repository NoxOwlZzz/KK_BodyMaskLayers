# Changelog

## 0.3.1 - 2026-09-09

- Restored skin visibility when Studio hides gloves, socks, shoes, or pantyhose using a clothing state that is not `3`.
- Applied the game's slot visibility rules consistently to native and imported masks, change detection, and diagnostics while retaining card and coordinate data.
- Added a Koikatsu Sunshine build sharing the same implementation and saved-data format, with separate game references and packaging.
- Included `Koikatsu Party.exe` in the KK launch filter while keeping the KKS process list separate and excluding VR.
- Party and Sunshine runtime validation remains pending; Sunshine does not support partial mask transfers through Coordinate Load Option.

## 0.3.0 - 2026-09-04

- Made compatible metadata loading, conversion, indexing, and coexistence automatic instead of separate configuration modes.
- Moved character mask processing to late-frame execution so clothing visibility changes are composed before rendering.
- Renamed the compatibility subsystem around external and imported data while preserving serialized provider IDs, fingerprints, and schema values.
- Preserved pending composition, source resolution, and automatic-import persistence after transient failures.
- Removed unused internal helpers and an unused TextMeshPro build reference.

## 0.2.0 - 2026-08-22

- Added direct compatibility for declared Sideloader body-mask metadata and PNG/AssetBundle textures without requiring another DLL or a card/coordinate resave.
- Added continuous 8-bit state coverage, categorical bitset fast paths, gradient-aware decoding, binary nearest-neighbor and continuous bilinear resampling, and max-hide composition with upstream B/A preservation.
- Added the BML2 schema for interpretation/provenance/fingerprint metadata while retaining BML1 reads and original PNG bytes.
- Added an incremental session manifest catalog, generation negative cache, bounded compiled-mask LRU, deterministic fingerprints and native/external duplicate suppression.
- Added silent automatic conversion of resolved external masks into portable native BML2 layers, with no source-specific Maker UI or prompts and no overwrite of existing native layers.
- Added numeric diagnostic counters, coalesced per-frame scheduling and unchanged-output upload suppression.
- Fixed native and converted mask persistence across card, outfit and coordinate save/reload paths.
- Fixed binding to slot-and-item identity, removed the unsafe slot-only mode and retained explicit rebinding for deliberate garment replacements.
- Removed configurable PNG size controls while retaining the fixed 32 MiB format cap.
- Split persistence, native decode, clothing runtime, composition, diagnostics and external compatibility into cohesive owned components.
- Configured Release assemblies to omit debug symbols.
- Added source-only repository exclusions and parameterized game-directory scripts.

## 0.1.2

- Replaced the generic empty-slot rejection with an explicit `Equip an item before loading a mask` state and disabled Load until that clothing slot has a bindable item.
- Reused decoded masks by SHA-256 and generated previews from semantic data instead of decoding the PNG again.
- Reduced runtime composition work with same-resolution fast paths, state/category skips, duplicate-mask coalescing, cached UI lookups, and retained output buffers across temporary Off or disabled states.

## 0.1.1

- Moved the Maker UI into the nine stock clothing tabs with previews and state feedback.
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
