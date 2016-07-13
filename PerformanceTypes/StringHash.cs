using System;

namespace PerformanceTypes
{
    /// <summary>
    /// Allows a string to be hashed character by character using the FNV-1a hashing algorithm.
    /// Always use StringHash.Begin() or StringHash.GetHash() to instantiate.
    /// </summary>
    public struct StringHash : IEquatable<StringHash>
    {
        const uint FNV_PRIME = 16777619;
        const uint FNV_OFFSET_BASIS = 2166136261;

        public uint Value { get; private set; }

        public static StringHash Begin()
        {
            var hash = new StringHash();
            hash.Value = FNV_OFFSET_BASIS;
            return hash;
        }

        public void Iterate(char c)
        {
            Value = unchecked((c ^ Value) * FNV_PRIME);
        }

        public static StringHash GetHash(string s)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            var hash = Begin();
            foreach(var c in s)
                hash.Iterate(c);

            return hash;
        }

        public static StringHash GetHash(char[] buffer, int start, int length)
        {
            AssertBufferArgumentsAreSane(buffer.Length, start, length);

            var end = start + length;
            var hash = Begin();
            for (var i = start; i < end; i++)
            {
                hash.Iterate(buffer[i]);
            }

            return hash;
        }

        internal static void AssertBufferArgumentsAreSane(int bufferLength, int start, int length)
        {
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start), "Start argument cannot be negative");

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length argument cannot be negative");

            if (start + length > bufferLength)
                throw new Exception($"Start ({start}) plus length ({length}) arguments must be less than or equal to buffer.Length ({bufferLength}).");
        }

        public static bool operator ==(StringHash a, StringHash b)
        {
            return a.Value == b.Value;
        }

        public static bool operator !=(StringHash a, StringHash b)
        {
            return a.Value != b.Value;
        }

        public bool Equals(StringHash other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            return obj is StringHash && Equals((StringHash)obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}