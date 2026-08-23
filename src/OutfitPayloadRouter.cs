using System;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class OutfitPayloadRouter<TPayload>
        where TPayload : class
    {
        private readonly Func<int> getActiveCoordinateIndex;
        private readonly Func<int, bool> isCoordinateIndexValid;
        private readonly Func<int, TPayload> readIndexed;
        private readonly Action<TPayload> writeTransient;
        private readonly Action<TPayload, int> writeIndexed;

        public OutfitPayloadRouter(
            Func<int> activeCoordinateIndex,
            Func<int, bool> coordinateIndexValid,
            Func<int, TPayload> indexedReader,
            Action<TPayload> transientWriter,
            Action<TPayload, int> indexedWriter)
        {
            if (activeCoordinateIndex == null)
            {
                throw new ArgumentNullException("activeCoordinateIndex");
            }

            if (coordinateIndexValid == null)
            {
                throw new ArgumentNullException("coordinateIndexValid");
            }

            if (indexedReader == null)
            {
                throw new ArgumentNullException("indexedReader");
            }

            if (transientWriter == null)
            {
                throw new ArgumentNullException("transientWriter");
            }

            if (indexedWriter == null)
            {
                throw new ArgumentNullException("indexedWriter");
            }

            getActiveCoordinateIndex = activeCoordinateIndex;
            isCoordinateIndexValid = coordinateIndexValid;
            readIndexed = indexedReader;
            writeTransient = transientWriter;
            writeIndexed = indexedWriter;
        }

        public TPayload ReadActive(Func<TPayload> fallbackReader)
        {
            if (fallbackReader == null)
            {
                throw new ArgumentNullException("fallbackReader");
            }

            int coordinateIndex = getActiveCoordinateIndex();
            if (isCoordinateIndexValid(coordinateIndex))
            {
                TPayload indexed = readIndexed(coordinateIndex);
                if (indexed != null)
                {
                    return indexed;
                }
            }

            return fallbackReader();
        }

        public void WriteActive(TPayload payload)
        {
            writeTransient(payload);

            int coordinateIndex = getActiveCoordinateIndex();
            if (isCoordinateIndexValid(coordinateIndex))
            {
                writeIndexed(payload, coordinateIndex);
            }
        }
    }
}
