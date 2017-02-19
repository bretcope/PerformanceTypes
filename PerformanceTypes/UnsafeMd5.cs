using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
            fixed (Md5Digest* digestPtr = &this)
            {
                var digestBytePtr = (byte*)digestPtr;
                var bytes = new byte[16];

                for (var i = 0; i < 16; i++)
                {
                    bytes[i] = digestBytePtr[i];
                }

                return bytes;
            }
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

        static readonly uint[] s_sines =
        {
            0xd76aa478, 0xe8c7b756, 0x242070db, 0xc1bdceee, 0xf57c0faf, 0x4787c62a, 0xa8304613, 0xfd469501,
            0x698098d8, 0x8b44f7af, 0xffff5bb1, 0x895cd7be, 0x6b901122, 0xfd987193, 0xa679438e, 0x49b40821,
            0xf61e2562, 0xc040b340, 0x265e5a51, 0xe9b6c7aa, 0xd62f105d, 0x02441453, 0xd8a1e681, 0xe7d3fbc8,
            0x21e1cde6, 0xc33707d6, 0xf4d50d87, 0x455a14ed, 0xa9e3e905, 0xfcefa3f8, 0x676f02d9, 0x8d2a4c8a,
            0xfffa3942, 0x8771f681, 0x6d9d6122, 0xfde5380c, 0xa4beea44, 0x4bdecfa9, 0xf6bb4b60, 0xbebfbc70,
            0x289b7ec6, 0xeaa127fa, 0xd4ef3085, 0x04881d05, 0xd9d4d039, 0xe6db99e5, 0x1fa27cf8, 0xc4ac5665,
            0xf4292244, 0x432aff97, 0xab9423a7, 0xfc93a039, 0x655b59c3, 0x8f0ccc92, 0xffeff47d, 0x85845dd1,
            0x6fa87e4f, 0xfe2ce6e0, 0xa3014314, 0x4e0811a1, 0xf7537e82, 0xbd3af235, 0x2ad7d2bb, 0xeb86d391,
        };

        static readonly int[] s_shifts =
        {
            7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
            5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
            4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
            6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21,
        };

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

                for (var i = 0; i < bytesPerBlock; i++)
                {
                    uint f;
                    int g;

                    if (i < 16)
                    {
                        f = (b & c) | (~b & d);
                        g = i;
                    }
                    else if (i < 32)
                    {
                        f = (d & b) | (~d & c);
                        g = (5 * i + 1) % 16;
                    }
                    else if (i < 48)
                    {
                        f = b ^ c ^ d;
                        g = (3 * i + 5) % 16;
                    }
                    else
                    {
                        f = c ^ (b | ~d);
                        g = (7 * i) % 16;
                    }

                    var dTemp = d;
                    d = c;
                    c = b;

                    var k = s_sines[i];
                    var m = blockPtr[g];

                    b += LeftRotate(a + f + k + m, s_shifts[i]);
                    a = dTemp;
                }

                digest->A += a;
                digest->B += b;
                digest->C += c;
                digest->D += d;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint LeftRotate(uint value, int rotation)
        {
            return (value << rotation) | (value >> (32 - rotation));
        }
    }
}