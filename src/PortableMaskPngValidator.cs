namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class PortableMaskPngValidator : IMaskPngValidator
    {
        public MaskValidationResult Validate(byte[] pngBytes)
        {
            return PngMaskValidator.Validate(pngBytes);
        }
    }
}
