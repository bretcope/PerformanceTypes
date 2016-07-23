using System;
using System.Threading;

namespace PerformanceTypes
{
    public class StringSet
    {
        struct Slot
        {
            internal string Value;
            internal StringHash HashCode;
            internal int Next;
        }

        public struct StringSearchCursor
        {
            internal object Slots;
            internal StringHash Hash;
            internal int SlotIndex;

            /// <summary>
            /// Indicates that there are more strings to search. The next call to NextString() may still return null even if HasMore is true.
            /// NextString() will always return null if HasMore is false.
            /// </summary>
            public bool HasMore => SlotIndex >= 0;

            public string NextString()
            {
                var slots = (Slot[])Slots;

                while (HasMore)
                {
                    var index = SlotIndex;
                    var value = slots[index].Value;
                    var found = slots[index].HashCode == Hash;

                    // update to the next index in the linked list
                    SlotIndex = slots[index].Next;

                    if (found)
                        return value;
                }

                return null;
            }
        }

        class BucketsAndSlots
        {
            public readonly int[] Buckets;
            public readonly Slot[] Slots;
            public int NextAvailableSlotIndex;

            public BucketsAndSlots(int[] buckets, Slot[] slots, int nextAvailableSlotIndex)
            {
                Buckets = buckets;
                Slots = slots;
                NextAvailableSlotIndex = nextAvailableSlotIndex;
            }
        }

        // this extra indirection allows us to perform atomic grow operations (important to allow thread-safe reads without locking)
        BucketsAndSlots _data;

        readonly object _writeLock = new object();

        public int Count => _data.NextAvailableSlotIndex;
        public int MaxSize => _data.Slots.Length;

        public StringSet(int initialSize)
        {
            _data = new BucketsAndSlots(new int[initialSize], new Slot[initialSize], 0);
        }

        public StringSearchCursor GetSearchCursor(StringHash hash)
        {
            var data = _data;
            var cursor = new StringSearchCursor();
            cursor.Slots = data.Slots;
            cursor.Hash = hash;

            var buckets = data.Buckets;
            var bucket = hash.Value % buckets.Length;
            cursor.SlotIndex = buckets[bucket] - 1;

            return cursor;
        }
        
        public string GetFirstByHash(StringHash hash)
        {
            var cursor = GetSearchCursor(hash);
            return cursor.NextString();
        }

        public bool Add(string str, StringHash knownHashValue = default(StringHash))
        {
            if (knownHashValue == default(StringHash))
                knownHashValue = StringHash.GetHash(str);

            if (!ContainsString(str, knownHashValue))
            {
                lock (_writeLock)
                {
                    if (!ContainsString(str, knownHashValue))
                    {
                        AddImpl(str, knownHashValue);
                        return true;
                    }
                }
            }

            return false;
        }

        public bool Add(char[] buffer, int start, int length, out string str, StringHash knownHashValue = default(StringHash))
        {
            if (knownHashValue == default(StringHash))
                knownHashValue = StringHash.GetHash(buffer, start, length);
            else
                StringHash.AssertBufferArgumentsAreSane(buffer.Length, start, length);

            str = GetExistingStringImpl(buffer, start, length, knownHashValue);

            if (str != null)
                return false; // didn't add anything

            // an existing string wasn't found, we need to add it to the hash
            lock (_writeLock)
            {
                // first, check one more time to see if it exists
                str = GetExistingStringImpl(buffer, start, length, knownHashValue);

                if (str == null)
                {
                    // it definitely doesn't exist. Let's add it
                    str = new string(buffer, start, length);
                    AddImpl(str, knownHashValue);
                    return true;
                }

                return false;
            }
        }

        public string GetExistingString(char[] buffer, int start, int length, StringHash knownHashValue = default(StringHash))
        {
            if (knownHashValue == default(StringHash))
                knownHashValue = StringHash.GetHash(buffer, start, length);
            else
                StringHash.AssertBufferArgumentsAreSane(buffer.Length, start, length);

            return GetExistingStringImpl(buffer, start, length, knownHashValue);
        }

        string GetExistingStringImpl(char[] buffer, int start, int length, StringHash hash)
        {
            var cursor = GetSearchCursor(hash);
            while (cursor.HasMore)
            {
                var value = cursor.NextString();
                if (value != null && IsMatchingString(value, buffer, start, length))
                    return value;
            }

            return null;
        }

        bool ContainsString(string str, StringHash hash)
        {
            var cursor = GetSearchCursor(hash);
            while (cursor.HasMore)
            {
                if (str == cursor.NextString())
                    return true;
            }

            return false;
        }

        void AddImpl(string s, StringHash hash)
        {
            var data = _data;
            if (data.NextAvailableSlotIndex == data.Slots.Length)
            {
                Grow();
                data = _data;
            }

            var slots = data.Slots;
            var buckets = data.Buckets;

            var bucket = hash.Value % slots.Length;
            var slotIndex = data.NextAvailableSlotIndex;
            data.NextAvailableSlotIndex++;

            slots[slotIndex].Value = s;
            slots[slotIndex].HashCode = hash;
            slots[slotIndex].Next = buckets[bucket] - 1;

            // The hash set would no longer be thread-safe on reads if somehow the bucket got reassigned before the slot was setup
            Thread.MemoryBarrier();

            buckets[bucket] = slotIndex + 1;
        }

        void Grow()
        {
            var oldData = _data;
            var oldSize = oldData.Slots.Length;
            var newSize = oldSize * 2;

            var newSlots = new Slot[newSize];
            Array.Copy(oldData.Slots, newSlots, oldSize);

            var newBuckets = new int[newSize];
            for (var i = 0; i < oldSize; i++)
            {
                var bucket = newSlots[i].HashCode.Value % newSize;
                newSlots[i].Next = newBuckets[bucket] - 1;
                newBuckets[bucket] = i + 1;
            }

            _data = new BucketsAndSlots(newBuckets, newSlots, oldData.NextAvailableSlotIndex);
        }

        static bool IsMatchingString(string s, char[] buffer, int start, int length)
        {
            if (s.Length != length)
                return false;

            for (var i = start; i < length; i++)
            {
                if (s[i] != buffer[i])
                    return false;
            }

            return true;
        }
    }
}