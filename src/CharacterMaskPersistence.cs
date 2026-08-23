using System;
using ExtensibleSaveFormat;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class CharacterMaskPersistence
    {
        private readonly OutfitPayloadRouter<PluginData> payloadRouter;
        private readonly CharacterNativeMaskService nativeMasks;
        private ChaFileClothes observedClothes;

        public CharacterMaskPersistence(
            OutfitPayloadRouter<PluginData> router,
            CharacterNativeMaskService nativeMaskService)
        {
            if (router == null)
            {
                throw new ArgumentNullException("router");
            }

            if (nativeMaskService == null)
            {
                throw new ArgumentNullException("nativeMaskService");
            }

            payloadRouter = router;
            nativeMasks = nativeMaskService;
        }

        public void CaptureCurrentClothes(ChaControl character)
        {
            observedClothes = character == null || character.nowCoordinate == null
                ? null
                : character.nowCoordinate.clothes;
        }

        public void LoadCurrent(Func<PluginData> fallbackReader, string source)
        {
            nativeMasks.LoadPluginData(
                payloadRouter.ReadActive(fallbackReader),
                source);
        }

        public void LoadCoordinate(ChaFileCoordinate coordinate, string source)
        {
            nativeMasks.LoadPluginData(
                coordinate == null
                    ? null
                    : CoordinateDataHandler.ReadFromClothes(coordinate.clothes),
                source);
        }

        public void SaveCurrent()
        {
            PluginData data = CoordinateDataHandler.CreatePluginDataOrNull(
                nativeMasks.EnumerateOwnedLayers());
            payloadRouter.WriteActive(data);
        }

        public void SaveCoordinate(ChaFileCoordinate coordinate)
        {
            if (coordinate != null)
            {
                CoordinateDataHandler.WriteToClothes(
                    coordinate.clothes,
                    nativeMasks.EnumerateOwnedLayers());
            }
        }

        public bool TryCaptureCoordinateReplacement(ChaFileClothes clothes)
        {
            if (ReferenceEquals(clothes, observedClothes))
            {
                return false;
            }

            observedClothes = clothes;
            return true;
        }
    }
}
