namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    // Assembly-qualified names belong to the game target; serialized identities do not.
    internal static class GameTarget
    {
#if KKS
        public const string AssemblyName = "KKS_BodyMaskLayers";
        public const string AssemblyDescription = "Independent per-clothing-slot body alpha mask layers for Koikatsu Sunshine";
        public const string MainProcess = "KoikatsuSunshine.exe";
        public const string SideloaderAssembly = "KKS_Sideloader";
        public const string ForceHighPolyType = "KK_Plugins.ForceHighPoly, KKS_ForceHighPoly";
        public const string CoordinateLoadOptionGuid = "com.jim60105.kks.coordinateloadoption";
#else
        public const string AssemblyName = "KK_BodyMaskLayers";
        public const string AssemblyDescription = "Independent per-clothing-slot body alpha mask layers for Koikatsu";
        public const string MainProcess = "Koikatu.exe";
        public const string SideloaderAssembly = "Sideloader";
        public const string ForceHighPolyType = "KK_Plugins.ForceHighPoly, KK_ForceHighPoly";
        public const string CoordinateLoadOptionGuid = "com.jim60105.kk.coordinateloadoption";
#endif
        public const string SideloaderType = "Sideloader.Sideloader, " + SideloaderAssembly;
        public const string SideloaderResolverType =
            "Sideloader.AutoResolver.UniversalAutoResolver, " + SideloaderAssembly;
    }
}
