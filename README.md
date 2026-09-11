# BodyMask Layers

Independent body alpha masks for clothing in Koikatsu, Koikatsu Party, and Koikatsu Sunshine. Works in Maker, Studio, and the main game.

## Features

- Each clothing slot has its own mask, bound to the equipped item.
- Body visibility follows the garment's clothing state.
- Load, preview, enable, clear, export, and rebind masks from Maker.
- Masks are saved per outfit in character cards and coordinates.
- Compatible clothing-mask sources are imported automatically without another body-mask plugin.

## Requirements

Install the dependencies for your game:

- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases).
- [KKAPI or KKSAPI 1.42.2+](https://github.com/IllusionMods/IllusionModdingAPI/releases).
- ExtensibleSaveFormat 20.0+ from [BepisPlugins](https://github.com/IllusionMods/BepisPlugins/releases).

Sideloader is needed only to import masks declared by zipmods.

## Installation

1. Download the matching ZIP from [Releases](https://github.com/NoxOwlZzz/BodyMaskLayers/releases/latest): **KK** for Koikatsu / Koikatsu Party, or **KKS** for Koikatsu Sunshine.
2. Close the game and CharaStudio, then extract the ZIP into the game folder containing `BepInEx` and the game executable.
3. Keep only one BodyMask Layers DLL in that game's plugin folders, then restart the game.

Each ZIP contains the plugin DLL and a short requirements TXT. Install only the variant for your game.

## Usage

1. Equip a garment in Maker and open its clothing tab.
2. Find **Native body alpha mask** and select **Load new mask texture**.
3. Choose your PNG and use the checkbox to enable or disable the mask.
4. Save the character card or coordinate to store its masks.

Disabling a mask retains its image. **Clear mask texture** removes it, and **Export mask texture** saves the original PNG. Use **Bind mask to current item** to associate an existing mask with a replacement garment.

Each outfit can have different masks. In Sunshine, use complete coordinate loading to transfer them. KK also supports per-slot loading through Coordinate Load Option `21.1.4`.

## Creating masks

Use the character's body UV layout. Import a square, power-of-two PNG with 8-bit channels within the 32 MiB cap. Use opaque colors; image alpha does not control hiding.

| Color | Full | Partial |
|---|---|---|
| Yellow `#FFFF00` | Visible | Visible |
| Green `#00FF00` | Hidden | Visible |
| Black `#000000` or red `#FF0000` | Hidden | Hidden |

Hidden garments contribute no mask. For gradients with the default `Auto` mode, keep blue at zero and red less than or equal to green.

## Configuration

BepInEx creates the plugin's configuration on first launch. The default settings support categorical masks and compatible gradients. See [PluginConfig.cs](src/PluginConfig.cs) for the available settings.

<details>
<summary>For developers</summary>

Use Visual Studio 2022 or compatible MSBuild tooling. KK targets .NET Framework 3.5; KKS targets .NET Framework 4.6. From a Developer Command Prompt, supply the matching game folders:

```cmd
MSBuild BodyMaskLayers.proj /t:Build ^
  /p:KKGameRoot="<KK-game-root>" ^
  /p:KKSGameRoot="<KKS-game-root>"
```

The default builds both variants in Release. Use `/t:Package` to create the ZIPs in `dist`. Game references are not redistributed.

The saved-data contract is described in [DATA_FORMAT.md](DATA_FORMAT.md).

</details>

## Credits

NightOwlZzz / Owl. Uses BepInEx, HarmonyX, IllusionModdingAPI, ExtensibleSaveFormat, and Sideloader.
