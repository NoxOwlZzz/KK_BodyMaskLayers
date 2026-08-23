# KK_BodyMaskLayers

BodyMask Layers adds one independent body alpha mask PNG to each supported Koikatsu clothing slot. Every layer follows the state of its own garment, and active layers combine over the current vanilla or modded body mask.

- Version: `0.2.0`
- Plugin GUID: `com.nightowlzzz.koikatsu.bodymasklayers`
- Author: NightOwlZzz / Owl
- Supported processes: `Koikatu.exe` and `CharaStudio.exe`

## Features

- Independent masks for Top, Bottom, Bra, Shorts, Gloves, Pantyhose, Socks, Indoor Shoes, and Outdoor Shoes.
- Native Maker controls inside the nine stock clothing tabs.
- Load, preview, enable, clear, export, and bind actions for each layer.
- Clothing-state-aware Full, Partial, and Off behavior.
- Continuous 8-bit hide coverage with a compact bitset fast path for categorical masks.
- Direct Compatibility Mode for original `KK_ChaAlphaMask` clothing metadata and textures when its DLL is absent.
- Session metadata index, negative lookups, bounded compiled-mask LRU, and lazy source loading.
- Silent automatic conversion of resolved Nakay masks to portable native BML2 layers by default.
- Preservation of the upstream body mask and its packed blue/alpha channels.
- Per-outfit persistence through Extended Save.
- Card and coordinate transport of the original PNG bytes without storing the source file path.
- Fixed slot-and-item binding using Sideloader identity when available.
- Optional partial-coordinate integration for Coordinate Load Option `21.1.4`.
- Coalesced dirty requests, at most one composition per character/frame, output-change upload suppression, and cached Maker UI lookups.

## Requirements

- Koikatsu with BepInEx 5.
- KKAPI `1.42.2` or newer.
- ExtensibleSaveFormat `20.0` or newer.

Sideloader is required only for direct resolution of zipmod legacy sources. KCOX, ChaAlphaMask, Material Editor, Uncensor Selector, clothing-state menus, and Coordinate Load Option remain optional compatibility targets.

## Installation

Close Koikatsu and CharaStudio, then copy `KK_BodyMaskLayers.dll` to:

```text
<game-root>\BepInEx\plugins\KK_BodyMaskLayers\KK_BodyMaskLayers.dll
```

Restart the game after replacing the DLL. Do not hot-reload this plugin.

## Maker usage

1. Equip a garment in the intended slot.
2. Open that garment's stock Clothes tab.
3. Scroll to **Native body alpha mask**.
4. Select **Load new mask texture** and choose a valid PNG.
5. Confirm the preview and status message.
6. Cycle the available clothing states and inspect the result.
7. Save the character card or coordinate.

The Load action is unavailable when the selected slot has no bindable clothing item. **Bind mask to current item** should only be used after deliberately replacing a garment with compatible geometry.

Legacy Nakay sources do not add controls or prompts to Maker. With the default `Auto Convert Nakay Legacy Masks=true`, a successfully resolved source is silently copied into a portable native BML2 layer without modifying the zipmod or overwriting an existing native layer. Save the card or coordinate normally to retain that copy. Set the option to `false` when runtime-only compatibility without automatic persistence is preferred.

## Saved data

Native and automatically converted masks are stored in the Extended Save data of each `ChaFileClothes` outfit. Different outfits in one card can therefore use different masks. Coordinate files carry the masks for their outfit, and character cards carry the masks for every saved outfit. The original PNG bytes, enable state, interpretation mode, provenance/fingerprint, validation data, and item binding are stored; compiled buffers and local import paths are not. Automatic conversion happens only after a legacy source resolves and reaches disk only through a normal card/coordinate save. When automatic conversion is disabled, direct compatibility remains runtime-only and stores nothing.

## Mask palette

| Color | Full | Partial | Off |
|---|---:|---:|---:|
| Yellow `#FFFF00` | visible | visible | visible |
| Green `#00FF00` | hidden | visible | visible |
| Black `#000000` | hidden | hidden | visible |
| Red `#FF0000` | hidden | hidden | visible |

Exact palette colors remain bit-identical to 0.1.x behavior. In default `Auto`, compatible intermediate native R/G values are preserved as Full/Partial visibility and decoded once into 8-bit coverage; B data is rejected as ambiguous/packed instead of silently reinterpreted. Binary masks resample category-aware with nearest neighbor, while continuous state planes use bilinear interpolation after decoding. PNG imports remain square, power-of-two and 8-bit. Fixed internal format guards accept dimensions from 1 through 4096 pixels and embedded PNG data up to 32 MiB; these guards are not exposed as user settings. See [MASK_AUTHORING.md](MASK_AUTHORING.md) for details.

`Gradient Handling` is captured in each native layer when it is imported. Changing the global default affects new imports, not already stored BML2 layers; reimport a source deliberately to reinterpret it. BML1 layers migrate as `StrictCategorical` so existing cards retain their previous behavior.

## Configuration

The live configuration is created at:

```text
<game-root>\BepInEx\config\com.nightowlzzz.koikatsu.bodymasklayers.cfg
```

| Setting | Default | Purpose |
|---|---:|---|
| `Enabled` | `true` | Global contribution switch |
| `ColorTolerance` | `12` | Threshold RGB tolerance |
| `UnknownColorPolicy` | `RejectMask` | Handling of unsupported colors |
| `Gradient Handling` | `Auto` | Preserve safe R/G gradients or select strict categorical behavior |
| `UnknownStatePolicy` | `NoContribution` | Handling of clothing states outside `0..3` |
| `Enable Nakay Legacy Compatibility` | `true` | Direct legacy provider when the old DLL is absent |
| `Auto Convert Nakay Legacy Masks` | `true` | Silently create portable native copies of resolved legacy masks |
| `Legacy Index Auto Refresh` | `true` | Build the session manifest index once at startup |
| `Legacy Cache Memory Limit MB` | `128` | Bounded shared compiled-mask LRU |
| `Legacy Diagnostics` | `false` | Numeric provider/cache counters in explicit dumps |
| `DebugLogging` | `false` | Detailed diagnostic logging |
| `LogStateChanges` | `false` | Clothing-state logging |
| `LogComposition` | `false` | Composition timing logging |
| `DumpDiagnosticsShortcut` | `F8 + LeftControl` | Write a diagnostic snapshot |

A complete example is available in [`config/com.nightowlzzz.koikatsu.bodymasklayers.cfg.example`](config/com.nightowlzzz.koikatsu.bodymasklayers.cfg.example).

Mask binding is always slot-and-item: replacing a garment leaves its stored mask inactive until **Bind mask to current item** is used deliberately. Resolution and embedded-PNG size safety guards are fixed internal format limits rather than configuration options.

## Build and tests

The project targets .NET Framework 3.5 and expects local compile references copied from a valid game installation. Those DLLs are intentionally excluded from the repository and from release packages.

```bat
copy-references.bat "C:\path\to\Koikatsu"
build-release.bat
run-tests.bat
```

`KOIKATSU_DIR` may be set instead of passing the game directory to the scripts. Visual Studio 2022 or compatible MSBuild tooling is required.

Release builds intentionally omit debug symbols and local PDB paths.

## Compatibility and limitations

- Direct Nakay compatibility reads `<ChaAlphaMask><mask>` entries already exposed by Sideloader manifests, resolves original IDs through Sideloader, and loads only the declared PNG or AssetBundle texture. It supports Bottom, Bra, Shorts, Gloves, Pantyhose, Socks, and the selected Indoor/Outdoor Shoes source, including integrated `botMask` and object-option constraints.
- The binding chain is equipped clothing category/local ID -> Sideloader GUID and original ID -> matching `guid/category/id/botMask/objOpt` manifest record -> `pngBodyMaskPath` or `abPath` plus `abBodyMaskName`. Exact GUID/original identity wins inside a resolved-ID bucket; incomplete legacy metadata uses a deterministic fallback.
- The audited old plugin is `KK_ChaAlphaMask` / `nakay.kk.ChaAlphaMask` `1.0.0`. When installed, it remains the upstream owner and the direct provider fast-returns to prevent double application. Unaudited installed versions are disabled conservatively unless explicitly allowed.
- Direct compatibility was implemented against the confirmed raw-state contract `0=R`, `1=G`, `2=B`, `3=Off`. BodyMask Layers combines decoded sources with maximum hide coverage to prevent overlapping soft edges from darkening; the old shader sums overlapping legacy channels, so exact parity for overlapping semi-transparent legacy sources remains a runtime comparison item.
- Automatic conversion is silent, preserves binding/provenance/fingerprint and continuous state coverage, never overwrites an existing native layer, and does not alter the original legacy source. Disabling it leaves direct compatibility runtime-only.
- The provider uses a light 0.5-second fallback poll only for game changes without a reliable event. Polling compares references, IDs, scalar values and states; it performs no pixel reads, manifest scans, IO, decode, hashing, preview generation or composition when unchanged.
- Direct legacy sources are applied only to effective high-poly characters. Native `ChaControl.hiPoly` and the optional audited `KK_ForceHighPoly.IsHiPoly` result are recognized; unresolved low-poly characters are left unchanged.
- Coordinate Load Option partial-slot integration is version-gated to `21.1.4`.
- High-resolution masks increase card size and composition cost.
- A body shader without `_AlphaMask`, `_alpha_a`, and `_alpha_b` is left untouched.
- Automatic mask generation, painting, accessories, KKS, and multi-mask stacks are not implemented.

Pure .NET tests cover a pinned BML1 fixture and BML2 migration, parsing/index reuse (including 2,048 synthetic manifests), binary/continuous decoding, 512/1024 resampling, overlap rules, output B/A preservation, bounded caches, warm compiled-state buffer reuse, scheduling and a synthetic 600-tick idle model. Runtime visual comparison with original cards/coordinates/zipmods, Studio/game lifecycle, UI layout and actual 600-frame Unity profiling still require manual in-game validation; this source update does not claim those results.

The binary data format is documented in [DATA_FORMAT.md](DATA_FORMAT.md). This repository does not redistribute game assemblies or third-party plugin DLLs.

## Credits

Built for the Koikatsu modding ecosystem using BepInEx, HarmonyX, IllusionModdingAPI/KKAPI, ExtensibleSaveFormat and Sideloader. Direct legacy interoperability is based on the public runtime contract of Nakay's Character Alpha Mask; no third-party DLL, mod, card, texture or AssetBundle is redistributed.
