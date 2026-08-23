using System;
using System.Collections.Generic;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal sealed class BoundedLruCache<TKey, TValue>
    {
        private sealed class Entry
        {
            public Entry(TKey key, TValue value, long sizeBytes)
            {
                Key = key;
                Value = value;
                SizeBytes = sizeBytes;
            }

            public readonly TKey Key;
            public readonly TValue Value;
            public readonly long SizeBytes;
        }

        private readonly Dictionary<TKey, LinkedListNode<Entry>> entries;
        private readonly LinkedList<Entry> recency = new LinkedList<Entry>();
        private readonly PerformanceCounters counters;
        private long currentBytes;
        private long reportedCounterBytes;
        private long evictionCount;

        public BoundedLruCache(long maxBytes)
            : this(maxBytes, null, null)
        {
        }

        public BoundedLruCache(
            long maxBytes,
            IEqualityComparer<TKey> comparer,
            PerformanceCounters performanceCounters)
        {
            if (maxBytes <= 0L)
            {
                throw new ArgumentOutOfRangeException("maxBytes");
            }

            MaxBytes = maxBytes;
            entries = comparer == null
                ? new Dictionary<TKey, LinkedListNode<Entry>>()
                : new Dictionary<TKey, LinkedListNode<Entry>>(comparer);
            counters = performanceCounters;
        }

        public long MaxBytes { get; private set; }

        public long CurrentBytes
        {
            get { return currentBytes; }
        }

        public int Count
        {
            get { return entries.Count; }
        }

        public long EvictionCount
        {
            get { return evictionCount; }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            SynchronizeMemoryCounter();

            LinkedListNode<Entry> node;
            if (!entries.TryGetValue(key, out node))
            {
                value = default(TValue);
                return false;
            }

            Touch(node);
            value = node.Value.Value;
            return true;
        }

        public bool TryPeekValue(TKey key, out TValue value)
        {
            SynchronizeMemoryCounter();

            LinkedListNode<Entry> node;
            if (!entries.TryGetValue(key, out node))
            {
                value = default(TValue);
                return false;
            }

            value = node.Value.Value;
            return true;
        }

        public bool Put(TKey key, TValue value, long sizeBytes)
        {
            if (sizeBytes <= 0L)
            {
                throw new ArgumentOutOfRangeException("sizeBytes");
            }

            LinkedListNode<Entry> existing;
            if (entries.TryGetValue(key, out existing))
            {
                RemoveNode(existing, false);
            }

            if (sizeBytes > MaxBytes)
            {
                SynchronizeMemoryCounter();
                return false;
            }

            while (currentBytes + sizeBytes > MaxBytes && recency.Last != null)
            {
                RemoveNode(recency.Last, true);
            }

            Entry entry = new Entry(key, value, sizeBytes);
            LinkedListNode<Entry> node = recency.AddFirst(entry);
            entries.Add(key, node);
            currentBytes += sizeBytes;
            SynchronizeMemoryCounter();
            return true;
        }

        public bool Remove(TKey key)
        {
            LinkedListNode<Entry> node;
            if (!entries.TryGetValue(key, out node))
            {
                SynchronizeMemoryCounter();
                return false;
            }

            RemoveNode(node, false);
            SynchronizeMemoryCounter();
            return true;
        }

        public void SetMemoryLimit(long maxBytes)
        {
            if (maxBytes <= 0L)
            {
                throw new ArgumentOutOfRangeException("maxBytes");
            }

            MaxBytes = maxBytes;
            while (currentBytes > MaxBytes && recency.Last != null)
            {
                RemoveNode(recency.Last, true);
            }

            SynchronizeMemoryCounter();
        }

        public void Clear()
        {
            entries.Clear();
            recency.Clear();
            currentBytes = 0L;
            SynchronizeMemoryCounter();
        }

        private void Touch(LinkedListNode<Entry> node)
        {
            if (node != recency.First)
            {
                recency.Remove(node);
                recency.AddFirst(node);
            }
        }

        private void RemoveNode(LinkedListNode<Entry> node, bool isEviction)
        {
            entries.Remove(node.Value.Key);
            recency.Remove(node);
            currentBytes -= node.Value.SizeBytes;

            if (isEviction)
            {
                evictionCount++;
                if (counters != null)
                {
                    counters.Increment(PerformanceCounter.CacheEvictions);
                }
            }
        }

        private void SynchronizeMemoryCounter()
        {
            if (counters == null || !counters.Enabled)
            {
                return;
            }

            long delta = currentBytes - reportedCounterBytes;
            if (delta != 0L)
            {
                counters.AdjustCacheMemoryBytes(delta);
                reportedCounterBytes = currentBytes;
            }
        }
    }

    internal sealed class GenerationNegativeCache<TKey>
    {
        private readonly int maxEntries;
        private readonly Dictionary<TKey, LinkedListNode<TKey>> entries;
        private readonly LinkedList<TKey> recency = new LinkedList<TKey>();
        private readonly PerformanceCounters counters;
        private bool hasGeneration;
        private long generation;
        private long evictionCount;

        public GenerationNegativeCache(int maxEntries)
            : this(maxEntries, null, null)
        {
        }

        public GenerationNegativeCache(
            int maxEntries,
            IEqualityComparer<TKey> comparer,
            PerformanceCounters performanceCounters)
        {
            if (maxEntries <= 0)
            {
                throw new ArgumentOutOfRangeException("maxEntries");
            }

            this.maxEntries = maxEntries;
            entries = comparer == null
                ? new Dictionary<TKey, LinkedListNode<TKey>>()
                : new Dictionary<TKey, LinkedListNode<TKey>>(comparer);
            counters = performanceCounters;
        }

        public int Count
        {
            get { return entries.Count; }
        }

        public long Generation
        {
            get { return generation; }
        }

        public long EvictionCount
        {
            get { return evictionCount; }
        }

        public void BeginGeneration(long value)
        {
            if (hasGeneration && generation == value)
            {
                return;
            }

            entries.Clear();
            recency.Clear();
            generation = value;
            hasGeneration = true;
        }

        public bool Contains(TKey key, long currentGeneration)
        {
            BeginGeneration(currentGeneration);

            LinkedListNode<TKey> node;
            if (!entries.TryGetValue(key, out node))
            {
                return false;
            }

            if (node != recency.First)
            {
                recency.Remove(node);
                recency.AddFirst(node);
            }

            if (counters != null)
            {
                counters.Increment(PerformanceCounter.NegativeCacheHits);
            }

            return true;
        }

        public void Add(TKey key, long currentGeneration)
        {
            BeginGeneration(currentGeneration);

            LinkedListNode<TKey> existing;
            if (entries.TryGetValue(key, out existing))
            {
                if (existing != recency.First)
                {
                    recency.Remove(existing);
                    recency.AddFirst(existing);
                }

                return;
            }

            if (entries.Count >= maxEntries)
            {
                LinkedListNode<TKey> oldest = recency.Last;
                entries.Remove(oldest.Value);
                recency.Remove(oldest);
                evictionCount++;
                if (counters != null)
                {
                    counters.Increment(PerformanceCounter.CacheEvictions);
                }
            }

            LinkedListNode<TKey> node = recency.AddFirst(key);
            entries.Add(key, node);
        }

        public void Clear()
        {
            entries.Clear();
            recency.Clear();
        }
    }
}
