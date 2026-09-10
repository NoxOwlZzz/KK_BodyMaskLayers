# Changelog

## 0.3.1 - 2026-09-09

- Native and imported masks follow each clothing slot's visibility rules in Maker and Studio.
- Separate KK and Koikatsu Sunshine builds share the implementation and saved-data format.
- The KK build supports `Koikatu.exe`, `Koikatsu Party.exe`, and `CharaStudio.exe`; the KKS build supports `KoikatsuSunshine.exe` and `CharaStudio.exe`.
- Sunshine mask data uses complete coordinate loading.

## 0.3.0 - 2026-09-04

- Automatic compatible-metadata loading, conversion, indexing, and coexistence.
- Late-frame mask composition applies clothing visibility before rendering.
- Composition, source resolution, and automatic-import persistence can resume after transient failures.

## 0.2.0 - 2026-08-22

- Direct compatibility with declared Sideloader body-mask metadata and PNG/AssetBundle textures without another mask plugin.
- Continuous 8-bit state coverage, gradient-aware decoding, and max-hide composition with upstream B/A preservation.
- BML2 interpretation, provenance, and fingerprint metadata, with BML1 reads and original PNG preservation.
- Incremental manifest indexing, bounded compiled-mask caching, and native/external duplicate suppression.
- Silent automatic conversion of resolved external masks into portable native layers without overwriting existing native layers.
- Diagnostic counters, coalesced scheduling, and unchanged-output upload suppression.
- Native and converted masks persist in cards, outfits, and coordinates.
- Slot-and-item binding with explicit rebinding for deliberate garment replacements.
- A fixed 32 MiB PNG cap protects format allocations.

## 0.1.2

- Mask loading requires a bindable item in the selected clothing slot.
- SHA-256-based decoded-mask reuse and previews generated from semantic mask data.
- Composition fast paths, state/category skips, duplicate-mask coalescing, cached UI lookups, and retained output buffers across inactive states.

## 0.1.1

- Maker controls in the stock clothing tabs, with previews and state feedback.
- Exported-green palette alias and bounded normalization for nearby exported or antialiased colors.
- Layer actions require a loaded mask; file-dialog callbacks respect Maker lifecycle.

## 0.1.0

- Nine slot-specific layers with independent clothing-state behavior and OR composition.
- Vanilla or external base-mask preservation, including blue and alpha channels.
- Maker controls for loading, clearing, exporting, binding, and enabling layers.
- Versioned outfit, card, and coordinate persistence for original PNG bytes.
- Optional Sideloader identity and partial Coordinate Load Option integration.
- Cooperative ordering with ChaAlphaMask.
- Diagnostics and .NET Framework 3.5 pure-logic tests.
