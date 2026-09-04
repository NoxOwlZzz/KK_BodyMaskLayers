namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public sealed class ExternalResolvedMask
    {
        public ExternalMaskDescriptor Descriptor;
        public SemanticMask SemanticMask;
        public MaskColorStatistics Statistics;
        public string ContentHash;
        public string Fingerprint;
        public int CatalogGeneration;

        public bool IsContinuous
        {
            get { return SemanticMask != null && !SemanticMask.IsBinary; }
        }

        public long StorageBytes
        {
            get { return SemanticMask == null ? 0L : SemanticMask.StorageBytes; }
        }
    }
}
