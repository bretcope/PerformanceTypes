using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace PerformanceTypes
{
    public class StringSet : IReadOnlyCollection<string>
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
            /// Indicates that there are more strings to search. However, the next call to NextString() may still return null even if MightHaveMore is true.
            /// NextString() will always return null if MightHaveMore is false.
            /// </summary>
            public bool MightHaveMore => SlotIndex >= 0;

            /// <summary>
            /// Returns the next string in the set with a matching Hash value, and advances the cursor. Returns null if there are no more matching strings.
            /// </summary>
            public string NextString()
            {
                var slots = (Slot[])Slots;

                while (MightHaveMore)
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

        /// <summary>
        /// Number of strings currently contained in the set.
        /// </summary>
        public int Count => _data.NextAvailableSlotIndex;
        /// <summary>
        /// The number of strings which can be held in the set before it will have to grow its internal data structures.
        /// </summary>
        public int MaxSize => _data.Slots.Length;

        /// <summary>
        /// Initializes an empty set.
        /// </summary>
        /// <param name="initialSize">The initial number of strings the set can contain before having to resize its internal datastructures.</param>
        public StringSet(int initialSize)
        {
            _data = new BucketsAndSlots(new int[initialSize], new Slot[initialSize], 0);
        }

        public IEnumerator<string> GetEnumerator()
        {
            var slots = _data.Slots;
            foreach (var slot in slots)
            {
                var str = slot.Value;
                if (str != null)
                    yield return str;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns a search cursor which allows you to iterate over every string in the set with the same StringHash.
        /// </summary>
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

        /// <summary>
        /// Adds a string to the set if it does not already exist.
        /// </summary>
        /// <param name="str">The string to add to the set.</param>
        /// <param name="knownHashValue">(optional) If the StringHash for str has already been calculated, you can provide it here to save re-calculation.</param>
        /// <returns>True if the string was added. False if the string already existed in the set.</returns>
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

        /// <summary>
        /// Adds a string to the set if it does not already exist.
        /// </summary>
        /// <param name="buffer">The character array which represents the string you want to add.</param>
        /// <param name="start">The index in the character array where your string starts.</param>
        /// <param name="count">The length of the string you want to add.</param>
        /// <param name="str">The string object representation of the characters. A new string is only allocated when it does not already exist in the set.</param>
        /// <param name="knownHashValue">(optional) If the StringHash has already been calculated, you can provide it here to save re-calculation.</param>
        /// <returns>True if the string was added. False if the string already existed in the set.</returns>
        public bool Add(char[] buffer, int start, int count, out string str, StringHash knownHashValue = default(StringHash))
        {
            if (knownHashValue == default(StringHash))
                knownHashValue = StringHash.GetHash(buffer, start, count);
            else
                StringHash.AssertBufferArgumentsAreSane(buffer.Length, start, count);

            str = GetExistingStringImpl(buffer, start, count, knownHashValue);

            if (str != null)
                return false; // didn't add anything

            // an existing string wasn't found, we need to add it to the hash
            lock (_writeLock)
            {
                // first, check one more time to see if it exists
                str = GetExistingStringImpl(buffer, start, count, knownHashValue);

                if (str == null)
                {
                    // it definitely doesn't exist. Let's add it
                    str = new string(buffer, start, count);
                    AddImpl(str, knownHashValue);
                    return true;
                }

                return false;
            }
        }

        public unsafe bool Add(char* chars, int count, out string str, StringHash knownHashValue = default(StringHash))
        {
            if (knownHashValue == default(StringHash))
                knownHashValue = StringHash.GetHash(chars, count);

            str = GetExistingString(chars, count, knownHashValue);

            if (str != null)
                return false; // didn't add anything

            // an existing string wasn't found, we need to add it to the hash
            lock (_writeLock)
            {
                // first, check one more time to see if it exists
                str = GetExistingString(chars, count, knownHashValue);

                if (str == null)
                {
                    // it definitely doesn't exist. Let's add it
                    str = new string(chars, 0, count);
                    AddImpl(str, knownHashValue);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Uses the characters from a buffer to check whether a string exists in the set, and retrieve it if so.
        /// </summary>
        /// <param name="buffer">The character array which represents the string you want to check for.</param>
        /// <param name="start">The index in the character array where your string starts.</param>
        /// <param name="count">The length of the string you want to check for.</param>
        /// <param name="knownHashValue">(optional) If the StringHash has already been calculated, you can provide it here to save re-calculation.</param>
        /// <returns>If found in the set, the existing string is returned. If not found, null is returned.</returns>
        public string GetExistingString(char[] buffer, int start, int count, StringHash knownHashValue = default(StringHash))
        {
            if (knownHashValue == default(StringHash))
                knownHashValue = StringHash.GetHash(buffer, start, count);
            else
                StringHash.AssertBufferArgumentsAreSane(buffer.Length, start, count);

            return GetExistingStringImpl(buffer, start, count, knownHashValue);
        }

        public unsafe string GetExistingString(char* chars, int count, StringHash knownHashValue = default(StringHash))
        {
            if (knownHashValue == default(StringHash))
                knownHashValue = StringHash.GetHash(chars, count);

            var cursor = GetSearchCursor(knownHashValue);
            while (cursor.MightHaveMore)
            {
                var value = cursor.NextString();
                if (value != null && UnsafeStringComparer.AreEqual(value, chars, count))
                    return value;
            }

            return null;
        }

        string GetExistingStringImpl(char[] buffer, int start, int length, StringHash hash)
        {
            var cursor = GetSearchCursor(hash);
            while (cursor.MightHaveMore)
            {
                var value = cursor.NextString();
                if (value != null && UnsafeStringComparer.AreEqual(value, buffer, start, length))
                    return value;
            }

            return null;
        }

        bool ContainsString(string str, StringHash hash)
        {
            var cursor = GetSearchCursor(hash);
            while (cursor.MightHaveMore)
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
    }
}