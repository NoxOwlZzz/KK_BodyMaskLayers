# KK_BodyMaskLayers

BodyMask Layers adds one independent body alpha mask PNG to each supported Koikatsu clothing slot. Every layer follows the state of its own garment, and active layers combine over the current vanilla or modded body mask.

- Version: `0.1.2`
- Plugin GUID: `com.nightowlzzz.koikatsu.bodymasklayers`
- Author: NightOwlZzz / Owl
- Supported processes: `Koikatu.exe` and `CharaStudio.exe`

## Features

- Independent masks for Top, Bottom, Bra, Shorts, Gloves, Pantyhose, Socks, Indoor Shoes, and Outdoor Shoes.
- Native Maker controls inside the nine stock clothing tabs.
- Load, preview, enable, clear, export, and bind actions for each layer.
- Clothing-state-aware Full, Partial, and Off behavior.
- Boolean OR composition across simultaneous layers.
- Preservation of the upstream body mask and its packed blue/alpha channels.
- Per-outfit persistence through Extended Save.
- Card and coordinate transport of the original PNG bytes without storing the source file path.
- Safe item binding using Sideloader identity when available.
- Optional partial-coordinate integration for Coordinate Load Option `21.1.4`.
- Dirty-only composition, decoded-mask reuse, and cached Maker UI lookups.

## Requirements

- Koikatsu with BepInEx 5.
- KKAPI `1.42.2` or newer.
- ExtensibleSaveFormat `20.0` or newer.

Sideloader, KCOX, ChaAlphaMask, Material Editor, Uncensor Selector, clothing-state menus, and Coordinate Load Option are optional compatibility targets.

## Installation

Close Koikatsu and CharaStudio, then copy `KK_BodyMaskLayers.dll` to:

```text
<game-root>\BepInEx\plugins\KK_BodyMaskLayers\KK_BodyMaskLayers.dll
```

Restart the game after replacing the DLL. Do not hot-reload this plugin.

## Maker usage

1. Equip a garment in the intended slot.
2. Open that garment's stock Clothes tab.
3. Scroll to **Body alpha mask**.
4. Select **Load new mask texture** and choose a valid PNG.
5. Confirm the preview and status message.
6. Cycle the available clothing states and inspect the result.
7. Save the character card or coordinate.

The Load action is unavailable when the selected slot has no bindable clothing item. **Bind mask to current item** should only be used after deliberately replacing a garment with compatible geometry.

## Saved data

Masks are stored in the Extended Save data of each `ChaFileClothes` outfit. Different outfits in one card can therefore use different masks. Coordinate files carry the masks for their outfit, and character cards carry the masks for every saved outfit. The original PNG bytes, enable state, validation data, and item binding are stored; the local file path is not.

## Mask palette

| Color | Full | Partial | Off |
|---|---:|---:|---:|
| Yellow `#FFFF00` | visible | visible | visible |
| Green `#00FF00` | hidden | visible | visible |
| Black `#000000` | hidden | hidden | visible |
| Red `#FF0000` | hidden | hidden | visible |

The default policy rejects unsupported blue or unknown colors. Threshold mode recognizes the exported green alias `#4CFF00` and can normalize a small bounded amount of nearby antialiasing. PNGs must be square, power-of-two, 8-bit images. The default accepted range is 128 through 2048 pixels with a 16 MiB limit per layer. See [MASK_AUTHORING.md](MASK_AUTHORING.md) for details.

## Configuration

The live configuration is created at:

```text
<game-root>\BepInEx\config\com.nightowlzzz.koikatsu.bodymasklayers.cfg
```

| Setting | Default | Purpose |
|---|---:|---|
| `Enabled` | `true` | Global contribution switch |
| `MaximumMaskResolution` | `2048` | Maximum accepted and output dimension |
| `ColorTolerance` | `12` | Threshold RGB tolerance |
| `UnknownColorPolicy` | `RejectMask` | Handling of unsupported colors |
| `UnknownStatePolicy` | `NoContribution` | Handling of clothing states outside `0..3` |
| `MaskBindingMode` | `SlotAndItem` | Safe item binding; `SlotOnly` is advanced |
| `DebugLogging` | `false` | Detailed diagnostic logging |
| `LogStateChanges` | `false` | Clothing-state logging |
| `LogComposition` | `false` | Composition timing logging |
| `DumpDiagnosticsShortcut` | `F8 + LeftControl` | Write a diagnostic snapshot |

A complete example is available in [`config/com.nightowlzzz.koikatsu.bodymasklayers.cfg.example`](config/com.nightowlzzz.koikatsu.bodymasklayers.cfg.example).

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

- ChaAlphaMask composition is enabled automatically only for the audited `1.0.0` version unless the advanced compatibility option is enabled.
- Coordinate Load Option partial-slot integration is version-gated to `21.1.4`.
- `SlotOnly` binding can apply a mask to incompatible clothing geometry.
- High-resolution masks increase card size and composition cost.
- A body shader without `_AlphaMask`, `_alpha_a`, and `_alpha_b` is left untouched.
- Automatic mask generation, painting, accessories, KKS, and multi-mask stacks are not implemented.

The binary data format is documented in [DATA_FORMAT.md](DATA_FORMAT.md). This repository does not redistribute game assemblies or third-party plugin DLLs.

## Credits

Built for the Koikatsu modding ecosystem using BepInEx, HarmonyX, IllusionModdingAPI/KKAPI, and ExtensibleSaveFormat.
