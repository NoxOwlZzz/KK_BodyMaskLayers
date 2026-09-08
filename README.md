# BodyMask Layers (KK / KKS)

BodyMask Layers gives each supported clothing slot its own body alpha mask in Koikatsu (KK) and Koikatsu Sunshine (KKS). Every mask follows the state and identity of its garment, and active masks are composed over the current body mask. The two game builds share the same source and saved-data format.

- Version: `0.3.1`
- Plugin GUID: `com.nightowlzzz.koikatsu.bodymasklayers`
- Author: NightOwlZzz / Owl
- KK processes: `Koikatu.exe` and `CharaStudio.exe`
- KKS processes: `KoikatsuSunshine.exe` and `CharaStudio.exe`

The KKS build has been checked against the game's assemblies; Maker, Studio, and main-game runtime validation is still pending.

## Features

- Independent masks for Top, Bottom, Bra, Shorts, Gloves, Pantyhose, Socks, and Outdoor Shoes; KK also exposes Indoor Shoes in Maker.
- Maker controls inside the corresponding stock clothing tabs.
- Load, preview, enable, clear, export, and bind actions for each mask.
- Full, Partial, and Off behavior driven by the owning garment.
- Continuous hide coverage and a compact categorical-mask path.
- Automatic support for compatible clothing metadata and textures when no separate provider is installed.
- Silent import of compatible sources into portable BML2 outfit data.
- Preservation of the body mask supplied by the game or other plugins.
- Per-outfit card and coordinate persistence through Extended Save.
- Slot-and-item binding, using Sideloader identity when available.
- Partial-coordinate integration for the audited KK Coordinate Load Option `21.1.4`.
- Dirty-only composition, cached lookups, and unchanged-output upload suppression.
- Late-frame composition so clothing visibility changes reach the body mask before rendering.

## Requirements

Both variants require BepInEx 5 and the dependencies built for the same game:

| Game | Plugin DLL | Framework | API | Extended Save |
|---|---|---|---|---|
| Koikatsu | `KK_BodyMaskLayers.dll` | .NET Framework 3.5 | KKAPI `1.42.2` or newer | ExtensibleSaveFormat `20.0` or newer |
| Koikatsu Sunshine | `KKS_BodyMaskLayers.dll` | .NET Framework 4.6 | KKSAPI `1.42.2` or newer | KKS ExtensibleSaveFormat `20.0` or newer |

Sideloader is required only when resolving compatible textures declared by zipmods. KCOX, Material Editor, Uncensor Selector, clothing-state menus, and Coordinate Load Option are optional compatibility targets.

## Installation

Close the game and its CharaStudio. Extract the ZIP for that game into its root directory; it contains only `REQUISITOS.txt` and one plugin DLL:

```text
<game-root>\BepInEx\plugins\KK_BodyMaskLayers\KK_BodyMaskLayers.dll
<game-root>\BepInEx\plugins\KKS_BodyMaskLayers\KKS_BodyMaskLayers.dll
```

These are alternative installations, not two files to install together. Keep only the DLL for the selected game and remove any other copy of BodyMask Layers from that game's plugin folders. BepInEx and the dependencies are not included in the ZIP.

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

Both builds retain all nine serialized slot indices. KKS omits only the Indoor Shoes Maker controls; Outdoor Shoes remains slot `8`. Matching payload formats do not establish cross-game card or coordinate compatibility, which has not been verified.

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

## Building and packaging

Use Visual Studio 2022 or compatible MSBuild tooling. `KK_BodyMaskLayers.csproj` targets .NET Framework 3.5; `KKS_BodyMaskLayers.csproj` targets .NET Framework 4.6. Both import `BodyMaskLayers.Shared.projitems` and `BodyMaskLayers.Shared.targets`, so source, reference rules, and packaging are maintained once.

From the repository root in a Visual Studio Developer Command Prompt, supply the roots of the matching game installations:

```cmd
MSBuild BodyMaskLayers.proj /t:Rebuild /p:Game=Both /p:Configuration=Release /p:KKGameRoot="<KK-game-root>" /p:KKSGameRoot="<KKS-game-root>" /m /nologo
```

Replace the placeholders with installation directories. `Game` accepts `KK`, `KKS`, or `Both`; the aggregator defaults to `Both` and `Release`. Its targets are `Build`, `Rebuild`, `Clean`, and `Package`. Use `Configuration=Debug` for development. A single-game build can use `GameRoot` instead of its game-specific property.

| Reference source | KK | KKS |
|---|---|---|
| Managed game assemblies | `Koikatu_Data/Managed` | `KoikatsuSunshine_Data/Managed` |
| BepInEx / Harmony | `BepInEx/core` | `BepInEx/core` |
| API assembly | `BepInEx/plugins/KKAPI.dll` | `BepInEx/plugins/KKSAPI.dll` |
| Extended Save assembly | `BepInEx/plugins/KK_BepisPlugins/ExtensibleSaveFormat.dll` | `BepInEx/plugins/KKS_BepisPlugins/KKS_ExtensibleSaveFormat.dll` |

The KKS references include its separate UniRx and Unity modules. Its API assembly is named `KKSAPI`, but the API namespace remains `KKAPI`. The plugin's public namespace and GUID are shared deliberately; no game-specific namespace or schema rename is required.

For a different layout, override `ManagedReferencePath`, `BepInExReferencePath`, `ApiReferencePath`, or `ExtendedSaveReferencePath` on a single-game build. The latter two are file paths. Without an installation root, references are read from the flat `lib` directory for KK or `lib/KKS` for KKS; `GameReferencePath` overrides that directory. The exact reference list is in [BodyMaskLayers.Shared.projitems](BodyMaskLayers.Shared.projitems). Never mix references from the two games. References are excluded from Git and release output and must not be redistributed.

Release outputs are `bin/Release/KK_BodyMaskLayers.dll` and `bin/KKS/Release/KKS_BodyMaskLayers.dll`. Release builds reject warnings and check that the DLL exists without a PDB.

To package both variants, use the same game-root properties:

```cmd
MSBuild BodyMaskLayers.proj /t:Package /p:Game=Both /p:Configuration=Release /p:KKGameRoot="<KK-game-root>" /p:KKSGameRoot="<KKS-game-root>" /m /nologo
```

This writes separate `dist/KK_BodyMaskLayers_v0.3.1.zip` and `dist/KKS_BodyMaskLayers_v0.3.1.zip`. Each ZIP contains only its DLL under `BepInEx/plugins/<assembly-name>` and a brief `REQUISITOS.txt`. `PackageOutputPath` chooses another output directory; an existing ZIP is not overwritten unless `OverwritePackage=true` is explicitly supplied.

## Tests

The pure .NET suite uses the KK .NET 3.5 references `mscorlib.dll`, `System.dll`, `System.Core.dll`, and `System.Xml.dll` in `lib`. It does not load either game. Run each target's compile-time policy sequentially:

```cmd
MSBuild tests\KK_BodyMaskLayers.Tests.csproj /t:Rebuild /p:Configuration=Release /p:Game=KK /m /nologo
tests\bin\Release\KK_BodyMaskLayers.Tests.exe
MSBuild tests\KK_BodyMaskLayers.Tests.csproj /t:Rebuild /p:Configuration=Release /p:Game=KKS /m /nologo
tests\bin\Release\KK_BodyMaskLayers.Tests.exe
```

The existing pure .NET suite covers serialization, schema migration, binding, metadata parsing, categorical and continuous decoding, composition rules, caches, and scheduling. Runtime appearance and lifecycle behavior should also be checked in-game before publishing a release.

## Compatibility and limitations

KKS assembly/API checks establish the mappings below, not a completed runtime test. The KK build also needs the regression checks below after shared-source changes.

| Function | KK | KKS | Implementation | Validation |
|---|---|---|---|---|
| Maker mask controls | Nine clothing tabs | Eight tabs; no Indoor Shoes tab | Shared controls; target-specific tab availability | KKS API checked; runtime pending |
| Clothing-state updates | Nine slot indices | Same nine slot indices | Shared state normalization and composition; game hooks | KKS visibility IL checked; runtime pending |
| Card and coordinate data | Outfit-owned records | Same payload and ownership | Shared serializer and KKAPI callbacks | Pure round-trip tests; runtime round trips required |
| Studio and main game | KK process targets | KKS process targets | Shared character controller | KKS runtime pending; KK regression required |
| Compatible clothing sources | Sideloader / optional provider | KKS Sideloader / optional provider | Shared reader with target-specific assembly names | KKS API checked; runtime pending |
| CLO partial per-slot loading | Version `21.1.4` adapter | Not supported | Version-gated KK adapter only | KKS `21.12.23.0` can abort without reporting success |

- KKS users should load complete coordinates when using these masks. Its Coordinate Load Option partial-slot bridge is disabled because a cancelled clothing transfer must not replace mask data.
- The built-in reader understands `<ChaAlphaMask><mask>` entries exposed by Sideloader manifests and loads only the declared PNG or AssetBundle texture.
- It supports Bottom, Bra, Shorts, Gloves, Pantyhose, Socks, and the selected Indoor or Outdoor Shoes source, including integrated-bottom and object-option constraints.
- Matching uses the equipped category and item identity, Sideloader GUID/original ID, and the corresponding manifest record.
- If a separate provider is installed, its material writes become the upstream composition base and the built-in metadata reader remains idle to prevent duplicate ownership.
- Compatible sources use mask planes `0=R`, `1=G`, `2=B`, and `3=Off` and are combined with maximum hide coverage. Slot visibility takes precedence: gloves, socks, and shoes contribute only in state `0`; pantyhose contributes in states `0/1`.
- The fallback poll handles game changes without a reliable event and performs no image decoding, source IO, or composition while state is unchanged.
- Compatible source lookup applies to effective high-poly characters.
- A body shader without `_AlphaMask`, `_alpha_a`, and `_alpha_b` is left untouched.
- Automatic mask generation, painting, accessories, VR processes, and multiple masks within one clothing slot are outside the plugin's scope.
- Cross-game card and coordinate transfer is not verified; the shared mask schema does not migrate game clothing identities or coordinate layouts.

The binary data format is documented in [DATA_FORMAT.md](DATA_FORMAT.md). This repository does not redistribute game assemblies, third-party plugins, mods, cards, or textures.

### Runtime checks before release

Run the following checks separately in each game with only its matching plugin DLL installed. For a failure, capture `BepInEx/LogOutput.log` and press Left Ctrl + F8 while the incorrect state is visible. Snapshots are written under `BepInEx/config/BodyMaskLayers/Diagnostics`; include the action, slot, clothing state, and game/dependency versions. Enable `DebugLogging` for counter checks and disable it afterward. Review logs for personal information before sharing them.

| Action | Expected result | Additional evidence if it fails |
|---|---|---|
| Start Maker, import a categorical and a gradient mask, then enable, disable, export, and clear | Controls work; preview matches; export preserves the source PNG; disabling retains it | Import status and the smallest reproducible PNG |
| Change each garment through its available states, including hide/remove and rapid Top changes | Body coverage follows the current state with no visible stale frame | Snapshots before and after the failing transition |
| Save and reload two cards and multiple outfits with different masks | Each outfit retains only its own mask and item binding | Save/load steps and affected coordinate index |
| Save/load a complete coordinate; reload a card with Clothes excluded | Coordinate masks load; excluding Clothes preserves the current masks | Selected load flags and snapshots around the load |
| In Studio, use two characters, change clothing states, duplicate one, and save/reload the scene | Per-character masks remain independent and visibility updates immediately | Scene action sequence and affected character/slot |
| Change clothes in the main game; test a compatible source with and without its separate provider | Native masks update; compatible sources have one owner and no duplicate contribution | Compatibility section of the snapshot and mod identity |
| Re-enter Maker and remove/reload Studio characters repeatedly, then leave the scene idle | No duplicate controls, stale callbacks, repeated errors, or unnecessary composition | Diagnostic counters before and after idle |

KK regression checklist:

- [ ] Run the checks above against existing KK cards and coordinates.
- [ ] Check all nine Maker tabs and both shoe selections, including immediate Top and Studio visibility updates.
- [ ] With CLO `21.1.4`, verify selected slots replace/clear masks and unselected slots retain theirs.

KKS validation checklist (not yet completed):

- [ ] Run the checks above in Sunshine Maker, main game, and its CharaStudio.
- [ ] Verify eight Maker tabs, one Outdoor Shoes control block bound to slot `8`, and independent masks in the game's four coordinate types.
- [ ] Use complete coordinate loads; if CLO `21.12.23.0` is installed, confirm the partial-slot bridge is reported disabled and the core plugin still works.

## Credits

Built for the Koikatsu and Koikatsu Sunshine modding ecosystem using BepInEx, HarmonyX, IllusionModdingAPI (KKAPI/KKSAPI), ExtensibleSaveFormat, and Sideloader.

## License

No license has been selected for this repository. Public redistribution should wait until the copyright holder adds a `LICENSE` file.
