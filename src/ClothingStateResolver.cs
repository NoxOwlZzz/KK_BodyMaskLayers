namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class ClothingStateResolver
    {
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
