using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace PerformanceTypes
{
    /// <summary>
    /// Represents the result of an MD5 hash operation.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = SIZE)]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
    public struct Md5Digest : IEquatable<Md5Digest>
    {
        [FieldOffset(0)] internal uint A;
        [FieldOffset(4)] internal uint B;
        [FieldOffset(8)] internal uint C;
        [FieldOffset(12)] internal uint D;
        
        [FieldOffset(0)] ulong _ab;
        [FieldOffset(8)] ulong _cd;

        /// <summary>
        /// The size of an <see cref="Md5Digest"/> struct (in bytes).
        /// </summary>
        public const int SIZE = 16;

        /// <summary>
        /// Returns the raw bytes from the struct. Note that <see cref="Md5Digest"/> is primarily intended to be used in unsafe context where you can simply
        /// take the address of the struct to get a byte pointer. This method will cause an allocation on the managed heap.
        /// </summary>
        public unsafe byte[] GetBytes()
        {
            var bytes = new byte[SIZE];
            fixed (byte* ptr = bytes)
            {
                WriteBytes(ptr);
            }

            return bytes;
        }

        /// <summary>
        /// Writes the raw bytes from the digest into a byte buffer. The buffer must have at least 16 bytes available starting at <paramref name="index"/>. An
        /// exception is thrown when there are not enough bytes remaining.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="index">The index </param>
        /// <returns>Always returns 16 (the number of bytes written).</returns>
        public unsafe int WriteBytes(byte[] buffer, int index = 0)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (index + SIZE > buffer.Length)
                throw new InvalidOperationException($"Buffer is not large enough to write 16 bytes at index {index}. Length = {buffer.Length}");

            fixed (byte* ptr = buffer)
            {
                return WriteBytes(&ptr[index]);
            }
        }

        /// <summary>
        /// Writes the raw bytes from the digest into a byte buffer. The buffer must have at least 16 bytes available.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <returns>Always returns 16 (the number of bytes written).</returns>
        public unsafe int WriteBytes(byte* buffer)
        {
            var longPtr = (ulong*)buffer;

            longPtr[0] = _ab;
            longPtr[1] = _cd;

            return SIZE;
        }

        /// <summary>
        /// Returns true if both <see cref="Md5Digest"/> operands have the same value.
        /// </summary>
        public static bool operator ==(Md5Digest a, Md5Digest b)
        {
            return a._ab == b._ab && a._cd == b._cd;
        }


        /// <summary>
        /// Returns true if both <see cref="Md5Digest"/> operands do not have the same value.
        /// </summary>
        public static bool operator !=(Md5Digest a, Md5Digest b)
        {
            return !(a._ab == b._ab && a._cd == b._cd);
        }

        /// <summary>
        /// Returns true if <paramref name="other"/> has the same value as the current <see cref="Md5Digest"/>.
        /// </summary>
        public bool Equals(Md5Digest other)
        {
            return this == other;
        }

        /// <summary>
        /// Returns true if <paramref name="obj"/> is a <see cref="Md5Digest"/> and has the same value as the current <see cref="Md5Digest"/>.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is Md5Digest && Equals((Md5Digest)obj);
        }

        /// <summary>
        /// Returns the first 32-bits of the hash as a signed integer.
        /// </summary>
        public override int GetHashCode()
        {
            return (int)A;
        }

        /// <summary>
        /// Returns a hex representation of the digest.
        /// </summary>
        public override unsafe string ToString()
        {
            fixed (Md5Digest* digestPtr = &this)
            {
                return Unsafe.ToHexString((byte*)digestPtr, SIZE);
            }
        }
    }

    /// <summary>
    /// A collection of methods for calculating the MD5 of byte arrays without performing any managed heap allocations.
    /// </summary>
    public static class UnsafeMd5
    {
        [StructLayout(LayoutKind.Sequential)]
        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        struct Block
        {
#pragma warning disable 169 // unused field
            ulong _zero;
            ulong _one;
            ulong _two;
            ulong _three;
            ulong _four;
            ulong _five;
            ulong _six;
            ulong _seven;
#pragma warning restore 169

            public void SetOriginalLength(int bytesCount)
            {
                _seven = (ulong)bytesCount * 8;
            }
        }

        /// <summary>
        /// WARNING: this method will only work correctly on little-endian architectures.
        /// Calculates the MD5 hash of the input.
        /// </summary>
        /// <param name="input">The byte array to hash.</param>
        /// <param name="digest">The result of the hash function.</param>
        public static unsafe void ComputeHash(byte[] input, out Md5Digest digest)
        {
            fixed (Md5Digest* digestPtr = &digest)
            fixed (byte* ptr = input)
            {
                ComputeHash(ptr, input.Length, digestPtr);
            }
        }

        /// <summary>
        /// WARNING: this method will only work correctly on little-endian architectures.
        /// Calculates the MD5 hash of the input.
        /// </summary>
        /// <param name="input">The input (byte array) to hash.</param>
        /// <param name="length">The length of the input in bytes.</param>
        /// <param name="digest">The result of the hash function.</param>
        public static unsafe void ComputeHash(byte* input, int length, Md5Digest* digest)
        {
            const int bytesPerBlock = 64;
            var blocksCount = (length + 8) / bytesPerBlock + 1;

            digest->A = 0x67452301;
            digest->B = 0xefcdab89;
            digest->C = 0x98badcfe;
            digest->D = 0x10325476;

            var paddingBlockData = default(Block);

            for (var blockIndex = 0; blockIndex < blocksCount; blockIndex++)
            {
                var offset = blockIndex * bytesPerBlock;
                var blockEnd = offset + bytesPerBlock;

                uint* blockPtr;

                if (blockEnd > length) // we're going to run out of input on this block
                {
                    if (offset >= length) // we're already totally past the input at this point - this block is just padding, and definitely the final block
                    {
                        if (offset == length)
                        {
                            // the end of input perfectly lined up with a block - so this is the start of padding
                            *(byte*)&paddingBlockData = 0x80;
                        }
                        else
                        {
                            // this is not the start of padding, so we need to clear out any data from the first padding block
                            paddingBlockData = default(Block);
                        }

                        paddingBlockData.SetOriginalLength(length);
                    }
                    else // there is still some input left to consume before we get to padding
                    {
                        var bytesRemaining = bytesPerBlock - (blockEnd - length);
                        var paddingPtr = (byte*)&paddingBlockData;
                        Unsafe.MemoryCopy(&input[offset], paddingPtr, bytesRemaining);

                        // add bit
                        paddingPtr[bytesRemaining] = 0x80;

                        // check if we can add the length
                        if (bytesPerBlock - (bytesRemaining + 1) >= 8)
                            paddingBlockData.SetOriginalLength(length);

                    }

                    blockPtr = (uint*)&paddingBlockData;
                }
                else
                {
                    blockPtr = (uint*)&input[offset];
                }

                var a = digest->A;
                var b = digest->B;
                var c = digest->C;
                var d = digest->D;

                uint f;

                // 0 (a, b, c, d)
                f = (c ^ d) & b ^ d;
                a += f;
                a += 0xd76aa478;
                a += blockPtr[0];
                a = b + ((a << 7) | (a >> (32 - 7)));

                // 1 (d, a, b, c)
                f = (b ^ c) & a ^ c;
                d += f;
                d += 0xe8c7b756;
                d += blockPtr[1];
                d = a + ((d << 12) | (d >> (32 - 12)));

                // 2 (c, d, a, b)
                f = (a ^ b) & d ^ b;
                c += f;
                c += 0x242070db;
                c += blockPtr[2];
                c = d + ((c << 17) | (c >> (32 - 17)));

                // 3 (b, c, d, a)
                f = (d ^ a) & c ^ a;
                b += f;
                b += 0xc1bdceee;
                b += blockPtr[3];
                b = c + ((b << 22) | (b >> (32 - 22)));

                // 4 (a, b, c, d)
                f = (c ^ d) & b ^ d;
                a += f;
                a += 0xf57c0faf;
                a += blockPtr[4];
                a = b + ((a << 7) | (a >> (32 - 7)));

                // 5 (d, a, b, c)
                f = (b ^ c) & a ^ c;
                d += f;
                d += 0x4787c62a;
                d += blockPtr[5];
                d = a + ((d << 12) | (d >> (32 - 12)));

                // 6 (c, d, a, b)
                f = (a ^ b) & d ^ b;
                c += f;
                c += 0xa8304613;
                c += blockPtr[6];
                c = d + ((c << 17) | (c >> (32 - 17)));

                // 7 (b, c, d, a)
                f = (d ^ a) & c ^ a;
                b += f;
                b += 0xfd469501;
                b += blockPtr[7];
                b = c + ((b << 22) | (b >> (32 - 22)));

                // 8 (a, b, c, d)
                f = (c ^ d) & b ^ d;
                a += f;
                a += 0x698098d8;
                a += blockPtr[8];
                a = b + ((a << 7) | (a >> (32 - 7)));

                // 9 (d, a, b, c)
                f = (b ^ c) & a ^ c;
                d += f;
                d += 0x8b44f7af;
                d += blockPtr[9];
                d = a + ((d << 12) | (d >> (32 - 12)));

                // 10 (c, d, a, b)
                f = (a ^ b) & d ^ b;
                c += f;
                c += 0xffff5bb1;
                c += blockPtr[10];
                c = d + ((c << 17) | (c >> (32 - 17)));

                // 11 (b, c, d, a)
                f = (d ^ a) & c ^ a;
                b += f;
                b += 0x895cd7be;
                b += blockPtr[11];
                b = c + ((b << 22) | (b >> (32 - 22)));

                // 12 (a, b, c, d)
                f = (c ^ d) & b ^ d;
                a += f;
                a += 0x6b901122;
                a += blockPtr[12];
                a = b + ((a << 7) | (a >> (32 - 7)));

                // 13 (d, a, b, c)
                f = (b ^ c) & a ^ c;
                d += f;
                d += 0xfd987193;
                d += blockPtr[13];
                d = a + ((d << 12) | (d >> (32 - 12)));

                // 14 (c, d, a, b)
                f = (a ^ b) & d ^ b;
                c += f;
                c += 0xa679438e;
                c += blockPtr[14];
                c = d + ((c << 17) | (c >> (32 - 17)));

                // 15 (b, c, d, a)
                f = (d ^ a) & c ^ a;
                b += f;
                b += 0x49b40821;
                b += blockPtr[15];
                b = c + ((b << 22) | (b >> (32 - 22)));

                // 16 (a, b, c, d)
                f = (b ^ c) & d ^ c;
                a += f;
                a += 0xf61e2562;
                a += blockPtr[(5 * 16 + 1) & 0xf];
                a = b + ((a << 5) | (a >> (32 - 5)));

                // 17 (d, a, b, c)
                f = (a ^ b) & c ^ b;
                d += f;
                d += 0xc040b340;
                d += blockPtr[(5 * 17 + 1) & 0xf];
                d = a + ((d << 9) | (d >> (32 - 9)));

                // 18 (c, d, a, b)
                f = (d ^ a) & b ^ a;
                c += f;
                c += 0x265e5a51;
                c += blockPtr[(5 * 18 + 1) & 0xf];
                c = d + ((c << 14) | (c >> (32 - 14)));

                // 19 (b, c, d, a)
                f = (c ^ d) & a ^ d;
                b += f;
                b += 0xe9b6c7aa;
                b += blockPtr[(5 * 19 + 1) & 0xf];
                b = c + ((b << 20) | (b >> (32 - 20)));

                // 20 (a, b, c, d)
                f = (b ^ c) & d ^ c;
                a += f;
                a += 0xd62f105d;
                a += blockPtr[(5 * 20 + 1) & 0xf];
                a = b + ((a << 5) | (a >> (32 - 5)));

                // 21 (d, a, b, c)
                f = (a ^ b) & c ^ b;
                d += f;
                d += 0x2441453;
                d += blockPtr[(5 * 21 + 1) & 0xf];
                d = a + ((d << 9) | (d >> (32 - 9)));

                // 22 (c, d, a, b)
                f = (d ^ a) & b ^ a;
                c += f;
                c += 0xd8a1e681;
                c += blockPtr[(5 * 22 + 1) & 0xf];
                c = d + ((c << 14) | (c >> (32 - 14)));

                // 23 (b, c, d, a)
                f = (c ^ d) & a ^ d;
                b += f;
                b += 0xe7d3fbc8;
                b += blockPtr[(5 * 23 + 1) & 0xf];
                b = c + ((b << 20) | (b >> (32 - 20)));

                // 24 (a, b, c, d)
                f = (b ^ c) & d ^ c;
                a += f;
                a += 0x21e1cde6;
                a += blockPtr[(5 * 24 + 1) & 0xf];
                a = b + ((a << 5) | (a >> (32 - 5)));

                // 25 (d, a, b, c)
                f = (a ^ b) & c ^ b;
                d += f;
                d += 0xc33707d6;
                d += blockPtr[(5 * 25 + 1) & 0xf];
                d = a + ((d << 9) | (d >> (32 - 9)));

                // 26 (c, d, a, b)
                f = (d ^ a) & b ^ a;
                c += f;
                c += 0xf4d50d87;
                c += blockPtr[(5 * 26 + 1) & 0xf];
                c = d + ((c << 14) | (c >> (32 - 14)));

                // 27 (b, c, d, a)
                f = (c ^ d) & a ^ d;
                b += f;
                b += 0x455a14ed;
                b += blockPtr[(5 * 27 + 1) & 0xf];
                b = c + ((b << 20) | (b >> (32 - 20)));

                // 28 (a, b, c, d)
                f = (b ^ c) & d ^ c;
                a += f;
                a += 0xa9e3e905;
                a += blockPtr[(5 * 28 + 1) & 0xf];
                a = b + ((a << 5) | (a >> (32 - 5)));

                // 29 (d, a, b, c)
                f = (a ^ b) & c ^ b;
                d += f;
                d += 0xfcefa3f8;
                d += blockPtr[(5 * 29 + 1) & 0xf];
                d = a + ((d << 9) | (d >> (32 - 9)));

                // 30 (c, d, a, b)
                f = (d ^ a) & b ^ a;
                c += f;
                c += 0x676f02d9;
                c += blockPtr[(5 * 30 + 1) & 0xf];
                c = d + ((c << 14) | (c >> (32 - 14)));

                // 31 (b, c, d, a)
                f = (c ^ d) & a ^ d;
                b += f;
                b += 0x8d2a4c8a;
                b += blockPtr[(5 * 31 + 1) & 0xf];
                b = c + ((b << 20) | (b >> (32 - 20)));

                // 32 (a, b, c, d)
                f = b ^ c ^ d;
                a += f;
                a += 0xfffa3942;
                a += blockPtr[(3 * 32 + 5) & 0xf];
                a = b + ((a << 4) | (a >> (32 - 4)));

                // 33 (d, a, b, c)
                f = a ^ b ^ c;
                d += f;
                d += 0x8771f681;
                d += blockPtr[(3 * 33 + 5) & 0xf];
                d = a + ((d << 11) | (d >> (32 - 11)));

                // 34 (c, d, a, b)
                f = d ^ a ^ b;
                c += f;
                c += 0x6d9d6122;
                c += blockPtr[(3 * 34 + 5) & 0xf];
                c = d + ((c << 16) | (c >> (32 - 16)));

                // 35 (b, c, d, a)
                f = c ^ d ^ a;
                b += f;
                b += 0xfde5380c;
                b += blockPtr[(3 * 35 + 5) & 0xf];
                b = c + ((b << 23) | (b >> (32 - 23)));

                // 36 (a, b, c, d)
                f = b ^ c ^ d;
                a += f;
                a += 0xa4beea44;
                a += blockPtr[(3 * 36 + 5) & 0xf];
                a = b + ((a << 4) | (a >> (32 - 4)));

                // 37 (d, a, b, c)
                f = a ^ b ^ c;
                d += f;
                d += 0x4bdecfa9;
                d += blockPtr[(3 * 37 + 5) & 0xf];
                d = a + ((d << 11) | (d >> (32 - 11)));

                // 38 (c, d, a, b)
                f = d ^ a ^ b;
                c += f;
                c += 0xf6bb4b60;
                c += blockPtr[(3 * 38 + 5) & 0xf];
                c = d + ((c << 16) | (c >> (32 - 16)));

                // 39 (b, c, d, a)
                f = c ^ d ^ a;
                b += f;
                b += 0xbebfbc70;
                b += blockPtr[(3 * 39 + 5) & 0xf];
                b = c + ((b << 23) | (b >> (32 - 23)));

                // 40 (a, b, c, d)
                f = b ^ c ^ d;
                a += f;
                a += 0x289b7ec6;
                a += blockPtr[(3 * 40 + 5) & 0xf];
                a = b + ((a << 4) | (a >> (32 - 4)));

                // 41 (d, a, b, c)
                f = a ^ b ^ c;
                d += f;
                d += 0xeaa127fa;
                d += blockPtr[(3 * 41 + 5) & 0xf];
                d = a + ((d << 11) | (d >> (32 - 11)));

                // 42 (c, d, a, b)
                f = d ^ a ^ b;
                c += f;
                c += 0xd4ef3085;
                c += blockPtr[(3 * 42 + 5) & 0xf];
                c = d + ((c << 16) | (c >> (32 - 16)));

                // 43 (b, c, d, a)
                f = c ^ d ^ a;
                b += f;
                b += 0x4881d05;
                b += blockPtr[(3 * 43 + 5) & 0xf];
                b = c + ((b << 23) | (b >> (32 - 23)));

                // 44 (a, b, c, d)
                f = b ^ c ^ d;
                a += f;
                a += 0xd9d4d039;
                a += blockPtr[(3 * 44 + 5) & 0xf];
                a = b + ((a << 4) | (a >> (32 - 4)));

                // 45 (d, a, b, c)
                f = a ^ b ^ c;
                d += f;
                d += 0xe6db99e5;
                d += blockPtr[(3 * 45 + 5) & 0xf];
                d = a + ((d << 11) | (d >> (32 - 11)));

                // 46 (c, d, a, b)
                f = d ^ a ^ b;
                c += f;
                c += 0x1fa27cf8;
                c += blockPtr[(3 * 46 + 5) & 0xf];
                c = d + ((c << 16) | (c >> (32 - 16)));

                // 47 (b, c, d, a)
                f = c ^ d ^ a;
                b += f;
                b += 0xc4ac5665;
                b += blockPtr[(3 * 47 + 5) & 0xf];
                b = c + ((b << 23) | (b >> (32 - 23)));

                // 48 (a, b, c, d)
                f = c ^ (b | ~d);
                a += f;
                a += 0xf4292244;
                a += blockPtr[(7 * 48) & 0xf];
                a = b + ((a << 6) | (a >> (32 - 6)));

                // 49 (d, a, b, c)
                f = b ^ (a | ~c);
                d += f;
                d += 0x432aff97;
                d += blockPtr[(7 * 49) & 0xf];
                d = a + ((d << 10) | (d >> (32 - 10)));

                // 50 (c, d, a, b)
                f = a ^ (d | ~b);
                c += f;
                c += 0xab9423a7;
                c += blockPtr[(7 * 50) & 0xf];
                c = d + ((c << 15) | (c >> (32 - 15)));

                // 51 (b, c, d, a)
                f = d ^ (c | ~a);
                b += f;
                b += 0xfc93a039;
                b += blockPtr[(7 * 51) & 0xf];
                b = c + ((b << 21) | (b >> (32 - 21)));

                // 52 (a, b, c, d)
                f = c ^ (b | ~d);
                a += f;
                a += 0x655b59c3;
                a += blockPtr[(7 * 52) & 0xf];
                a = b + ((a << 6) | (a >> (32 - 6)));

                // 53 (d, a, b, c)
                f = b ^ (a | ~c);
                d += f;
                d += 0x8f0ccc92;
                d += blockPtr[(7 * 53) & 0xf];
                d = a + ((d << 10) | (d >> (32 - 10)));

                // 54 (c, d, a, b)
                f = a ^ (d | ~b);
                c += f;
                c += 0xffeff47d;
                c += blockPtr[(7 * 54) & 0xf];
                c = d + ((c << 15) | (c >> (32 - 15)));

                // 55 (b, c, d, a)
                f = d ^ (c | ~a);
                b += f;
                b += 0x85845dd1;
                b += blockPtr[(7 * 55) & 0xf];
                b = c + ((b << 21) | (b >> (32 - 21)));

                // 56 (a, b, c, d)
                f = c ^ (b | ~d);
                a += f;
                a += 0x6fa87e4f;
                a += blockPtr[(7 * 56) & 0xf];
                a = b + ((a << 6) | (a >> (32 - 6)));

                // 57 (d, a, b, c)
                f = b ^ (a | ~c);
                d += f;
                d += 0xfe2ce6e0;
                d += blockPtr[(7 * 57) & 0xf];
                d = a + ((d << 10) | (d >> (32 - 10)));

                // 58 (c, d, a, b)
                f = a ^ (d | ~b);
                c += f;
                c += 0xa3014314;
                c += blockPtr[(7 * 58) & 0xf];
                c = d + ((c << 15) | (c >> (32 - 15)));

                // 59 (b, c, d, a)
                f = d ^ (c | ~a);
                b += f;
                b += 0x4e0811a1;
                b += blockPtr[(7 * 59) & 0xf];
                b = c + ((b << 21) | (b >> (32 - 21)));

                // 60 (a, b, c, d)
                f = c ^ (b | ~d);
                a += f;
                a += 0xf7537e82;
                a += blockPtr[(7 * 60) & 0xf];
                a = b + ((a << 6) | (a >> (32 - 6)));

                // 61 (d, a, b, c)
                f = b ^ (a | ~c);
                d += f;
                d += 0xbd3af235;
                d += blockPtr[(7 * 61) & 0xf];
                d = a + ((d << 10) | (d >> (32 - 10)));

                // 62 (c, d, a, b)
                f = a ^ (d | ~b);
                c += f;
                c += 0x2ad7d2bb;
                c += blockPtr[(7 * 62) & 0xf];
                c = d + ((c << 15) | (c >> (32 - 15)));

                // 63 (b, c, d, a)
                f = d ^ (c | ~a);
                b += f;
                b += 0xeb86d391;
                b += blockPtr[(7 * 63) & 0xf];
                b = c + ((b << 21) | (b >> (32 - 21)));

                digest->A += a;
                digest->B += b;
                digest->C += c;
                digest->D += d;
            }
        }
    }
}