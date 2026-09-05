namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class ClothingStateResolver
    {
        // UpdateVisible hides binary slots at any nonzero state and pantyhose at 2/3.
        // Normalize the mask input without changing the game's stored clothing state.
        internal static byte NormalizeForSlot(ClothingSlot slot, byte rawState)
        {
            if (rawState != 1 && rawState != 2)
            {
                return rawState;
            }

            switch (slot)
            {
                case ClothingSlot.Gloves:
                case ClothingSlot.Socks:
                case ClothingSlot.IndoorShoes:
                case ClothingSlot.OutdoorShoes:
                    return 3;
                case ClothingSlot.Pantyhose:
                    return rawState == 2 ? (byte)3 : rawState;
                default:
                    return rawState;
            }
        }

        public static GarmentState Resolve(byte rawState)
        {
            switch (rawState)
            {
                case 0:
                    return GarmentState.Full;
                case 1:
                case 2:
                    return GarmentState.Partial;
                case 3:
                    return GarmentState.Off;
                default:
                    return GarmentState.Unknown;
            }
        }

        public static GarmentState ApplyUnknownPolicy(
            GarmentState state,
            UnknownStatePolicy policy,
            GarmentState lastKnown)
        {
            if (state != GarmentState.Unknown)
            {
                return state;
            }

            switch (policy)
            {
                case UnknownStatePolicy.TreatAsPartial:
                    return GarmentState.Partial;
                case UnknownStatePolicy.TreatAsFull:
                    return GarmentState.Full;
                case UnknownStatePolicy.PreserveLastKnown:
                    return lastKnown == GarmentState.Unknown ? GarmentState.Off : lastKnown;
                default:
                    return GarmentState.Off;
            }
        }
    }
}
