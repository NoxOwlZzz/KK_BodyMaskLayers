# KK_BodyMaskLayers

BodyMask Layers gives each supported Koikatsu clothing slot its own body alpha mask. Every mask follows the state and identity of its garment, and active masks are composed over the current body mask.

- Version: `0.3.1`
- Plugin GUID: `com.nightowlzzz.koikatsu.bodymasklayers`
- Author: NightOwlZzz / Owl
- Processes: `Koikatu.exe` and `CharaStudio.exe`

## Features

- Independent masks for Top, Bottom, Bra, Shorts, Gloves, Pantyhose, Socks, Indoor Shoes, and Outdoor Shoes.
- Maker controls inside the corresponding stock clothing tabs.
- Load, preview, enable, clear, export, and bind actions for each mask.
- Full, Partial, and Off behavior driven by the owning garment.
- Continuous hide coverage and a compact categorical-mask path.
- Automatic support for compatible clothing metadata and textures when no separate provider is installed.
- Silent import of compatible sources into portable BML2 outfit data.
- Preservation of the body mask supplied by the game or other plugins.
- Per-outfit card and coordinate persistence through Extended Save.
- Slot-and-item binding, using Sideloader identity when available.
- Partial-coordinate integration for Coordinate Load Option `21.1.4`.
- Dirty-only composition, cached lookups, and unchanged-output upload suppression.
- Late-frame composition so clothing visibility changes reach the body mask before rendering.

## Requirements

- Koikatsu with BepInEx 5.
- KKAPI `1.42.2` or newer.
- ExtensibleSaveFormat `20.0` or newer.

Sideloader is required only when resolving compatible textures declared by zipmods. KCOX, Material Editor, Uncensor Selector, clothing-state menus, and Coordinate Load Option are optional compatibility targets.

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
6. Inspect every available clothing state.
7. Save the character card or coordinate.

The Load action is unavailable when the slot has no bindable item. **Bind mask to current item** deliberately replaces the stored item identity; use it only after replacing a garment with compatible body coverage.

**Export mask texture** writes the original PNG byte-for-byte. **Clear mask texture** removes that slot's record; disabling the layer retains its image and binding.

Compatible external sources do not add controls or prompts. When resolved, they are imported automatically without modifying the source zipmod or overwriting a mask already assigned to that slot. A normal card or coordinate save persists the imported copy.

## Saved data

Each `ChaFileClothes` outfit owns its mask records. Different outfits in one card can therefore use different masks, coordinate files carry the current outfit's masks, and character cards carry the outfits saved by the game.

The payload stores the original PNG bytes, enable state, interpretation mode, source metadata, validation data, and slot-and-item binding. Runtime buffers and source file paths are not stored.

## Mask palette

Use the character's base-body UV layout, not the garment mesh UV. Import a square, power-of-two PNG with 8-bit channels (RGB, indexed, grayscale with alpha, or RGBA), within the 32 MiB cap. Source alpha does not control hiding; use opaque colors for clarity.

| Color | Full | Partial | Off |
|---|---:|---:|---:|
| Yellow `#FFFF00` | visible | visible | visible |
| Green `#00FF00` | hidden | visible | visible |
| Black `#000000` | hidden | hidden | visible |
| Red `#FF0000` | hidden | hidden | visible |

For pixels outside the exact palette, `Gradient Handling = Auto` accepts continuous coverage when `B=0` and `R<=G`: Full hiding is `255-R`, Partial is `255-G`, and Off contributes nothing. `PreserveContinuous` also accepts non-monotonic R/G coverage; `StrictCategorical` uses palette classification and tolerance. Unsupported colors follow `UnknownColorPolicy`.

Disable antialiasing for categorical masks or keep soft edges in compatible R/G space. Binary masks resize with nearest-neighbor sampling; continuous state planes use bilinear interpolation. Overlapping masks combine by maximum hide coverage.

The interpretation mode is stored with each imported layer. Changing the global default affects later imports; reimport a source to reinterpret an existing layer. Schema 1 payloads use `StrictCategorical` when read.

## Configuration

The live configuration is created under the game's BepInEx `config` directory.

| Setting | Default | Purpose |
|---|---:|---|
| `Enabled` | `true` | Global contribution switch |
| `Default output resolution` | `512` | Composition size when no upstream mask size is available |
| `Color classification` | `Threshold` | Categorical color classifier |
| `ColorTolerance` | `12` | Threshold RGB tolerance |
| `UnknownColorPolicy` | `RejectMask` | Handling of unsupported colors |
| `Gradient Handling` | `Auto` | Continuous or strict categorical interpretation |
| `UnknownStatePolicy` | `NoContribution` | Handling of clothing states outside `0..3` |
| `DebugLogging` | `false` | Detailed diagnostic logging and counters |
| `LogStateChanges` | `false` | Clothing-state logging |
| `LogComposition` | `false` | Composition timing logging |
| `LogIntervalSeconds` | `1` | Rate limit for repeated diagnostic messages |
| `DumpDiagnosticsShortcut` | `F8 + LeftControl` | Write a diagnostic snapshot to the log and config directory |

Compatible-source loading, indexing, import, and coexistence are automatic. Configuration sections, keys, and descriptions are defined in [`PluginConfig.cs`](src/PluginConfig.cs); BepInEx generates the configuration on first launch.

## Build and tests

The project targets .NET Framework 3.5 and requires Visual Studio 2022 or compatible MSBuild tooling. Create a `lib` directory at the repository root and copy these compile references from a valid game installation:

| Source within the game directory | DLLs to copy into `lib` |
|---|---|
| `Koikatu_Data/Managed` | `mscorlib.dll`, `System.dll`, `System.Core.dll`, `System.Xml.dll`, `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`, `UnityEngine.dll`, `UnityEngine.UI.dll` |
| `BepInEx/core` | `BepInEx.dll`, `0Harmony.dll` |
| `BepInEx/plugins` | `KKAPI.dll`, `ExtensibleSaveFormat.dll` (locate them in plugin subdirectories if needed) |

These references are excluded from Git and release output. Do not redistribute them.

From the repository root in a Visual Studio Developer Command Prompt, run:

```cmd
MSBuild KK_BodyMaskLayers.csproj /t:Rebuild /p:Configuration=Release /m /nologo
MSBuild tests\KK_BodyMaskLayers.Tests.csproj /t:Rebuild /p:Configuration=Release /m /nologo
tests\bin\Release\KK_BodyMaskLayers.Tests.exe
```

The plugin output is `bin/Release/KK_BodyMaskLayers.dll`; Release builds omit debug symbols. Verify that the DLL is present without a PDB before packaging it. The solution supports Debug builds for development.

The existing pure .NET suite covers serialization, schema migration, binding, metadata parsing, categorical and continuous decoding, composition rules, caches, and scheduling. Runtime appearance and lifecycle behavior should also be checked in-game before publishing a release.

## Compatibility and limitations

- The built-in reader understands `<ChaAlphaMask><mask>` entries exposed by Sideloader manifests and loads only the declared PNG or AssetBundle texture.
- It supports Bottom, Bra, Shorts, Gloves, Pantyhose, Socks, and the selected Indoor or Outdoor Shoes source, including integrated-bottom and object-option constraints.
- Matching uses the equipped category and item identity, Sideloader GUID/original ID, and the corresponding manifest record.
- If a separate provider is installed, its material writes become the upstream composition base and the built-in metadata reader remains idle to prevent duplicate ownership.
- Compatible sources use mask planes `0=R`, `1=G`, `2=B`, and `3=Off` and are combined with maximum hide coverage. Slot visibility takes precedence: gloves, socks, and shoes contribute only in state `0`; pantyhose contributes in states `0/1`.
- The fallback poll handles game changes without a reliable event and performs no image decoding, source IO, or composition while state is unchanged.
- Compatible source lookup applies to effective high-poly characters.
- A body shader without `_AlphaMask`, `_alpha_a`, and `_alpha_b` is left untouched.
- Automatic mask generation, painting, accessories, KKS, and multiple masks within one clothing slot are outside the plugin's scope.

The binary data format is documented in [DATA_FORMAT.md](DATA_FORMAT.md). This repository does not redistribute game assemblies, third-party plugins, mods, cards, or textures.

## Credits

Built for the Koikatsu modding ecosystem using BepInEx, HarmonyX, IllusionModdingAPI/KKAPI, ExtensibleSaveFormat, and Sideloader.
