using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PerformanceTypes
{
    /// <summary>
    /// Defines options for string-interning.
    /// </summary>
    public struct StringSetOptions
    {
        /// <summary>
        /// For any string whose encoded size (in bytes) is less than or equal to this value, a lookup in StringSet will be performed before allocating a new
        /// string. If the string already exists in StringSet, then no allocation occurs. If it does not exist in StringSet, or if its encoded size is larger
        /// than this value, then a new string is allocated. Newly allocated strings are NOT automatically added to StringSet unless
        /// PerformDangerousAutoAddToSet is true.
        /// 
        /// For performance reasons, it is recommended to use a small value, such as 256, or less. Use zero to disable StringSet lookups altogether.
        /// </summary>
        public int MaxEncodedSizeToLookupInSet { get; set; }
        /// <summary>
        /// NEVER use this option when reading user-provided or arbitrary input, unless you plan to recycle the StringSet (allow it to be garbage collected).
        /// Failing to follow that advice could lead to unbounded memory growth. It is recommended that you avoid this option unless you understand the
        /// implications and risks.
        /// Does not allocate a new string if it already exists in the StringSet. Newly allocated strings are automatically added to the StringSet.
        /// Only applies to strings whose encoded size is less than MaxEncodedSizeToLookupInSet (in bytes).
        /// </summary>
        public bool PerformDangerousAutoAddToSet { get; set; }
    }

    /// <summary>
    /// A stream implementation where the underlying data (a byte array) can be swapped out, and the stream can be reset for re-reading or writing.
    /// </summary>
    public partial class ReusableStream : Stream
    {
        /// <summary>
        /// The current index into <see cref="Data"/>. It is equal to <see cref="Offset"/> plus <see cref="Position"/>.
        /// </summary>
        int _realPosition;
        int _endPosition;  // offset + length used

        StringSetOptions _defaultStringSetOptions;

        /// <summary>
        /// The underlying data for the stream. To replace this underlying data, call <see cref="ReplaceData"/>.
        /// </summary>
        public byte[] Data { get; private set; }
        /// <summary>
        /// True if the stream can automatically resize 
        /// </summary>
        public bool CanGrow { get; private set; }

        /// <summary>
        /// True if the stream can be read from.
        /// </summary>
        public override bool CanRead => true;
        /// <summary>
        /// True if the stream can seek to a random byte location.
        /// </summary>
        public override bool CanSeek => true;
        /// <summary>
        /// True if the stream can be written to.
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// The total size that the stream could grow to without needing to resize its underlying data array.
        /// </summary>
        public long Capacity => Data.LongLength - Offset;
        /// <summary>
        /// The total length of the stream (bytes written).
        /// </summary>
        public override long Length => _endPosition - Offset;
        /// <summary>
        /// The offset into data which represents Position = 0 for the stream.
        /// </summary>
        public int Offset { get; private set; }
        /// <summary>
        /// The number of bytes which can be read before reaching the end of the stream.
        /// </summary>
        public int UnreadByteCount => _endPosition - _realPosition;

        /// <summary>
        /// Gets or sets the encoding used for reading and writing strings.
        /// </summary>
        public Encoding DefaultEncoding { get; set; } = Encoding.UTF8;
        /// <summary>
        /// A StringSet which can be used as a string intern pool. Usage of this is configured via the StringSetOptions struct which is passed to
        /// SetDefaultStringSetOptions(), or as an optional parameter to the ReadString() methods.
        /// </summary>
        public StringSet StringSet { get; set; }

        /// <summary>
        /// The current byte position of the stream. This value is not necessarily the same as the current index of the underlying data.
        /// </summary>
        public override long Position
        {
            get { return _realPosition - Offset; }
            set
            {
                if (value > Capacity)
                    throw new Exception("Position cannot be greater than capacity");

                _realPosition = Offset + (int)value;
            }
        }

        /// <summary>
        /// WARNING: This constructor creates a ReusableStream, but does NOT initialize an underlying data array. You MUST call ReplaceData() prior to using the stream.
        /// </summary>
        public ReusableStream()
        {
        }


        /// <summary>
        /// Creates a ReusableStream and initializes and underlying data array.
        /// </summary>
        /// <param name="capacity">Initial size (in bytes) of the underlying data array.</param>
        /// <param name="canGrow">True to allow the underlying data array to be automatically replaced when additional capacity is needed.</param>
        public ReusableStream(int capacity, bool canGrow = true)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            ReplaceData(new byte[capacity], 0, 0, canGrow);
        }

        /// <summary>
        /// Creates a ReusableStream using the provided data array.
        /// </summary>
        /// <param name="data">The array where data will be stored.</param>
        /// <param name="offset">The offset into data which represents Position = 0 for the stream.</param>
        /// <param name="length">
        /// The number of bytes after offset which represents meaningful data for the stream. Note: this value only affects reading. When writing, this length
        /// may be exceeded.
        /// </param>
        /// <param name="canGrow">True to allow the underlying data array to be automatically replaced when additional capacity is needed.</param>
        public ReusableStream(byte[] data, int offset, int length, bool canGrow = false)
        {
            ReplaceData(data, offset, length, canGrow);
        }

        /// <summary>
        /// Replaces the underlying data array and resets Position to zero.
        /// </summary>
        /// <param name="data">The replacement data array.</param>
        /// <param name="offset">The offset into data which represents Position = 0 for the stream.</param>
        /// <param name="length">
        /// The number of bytes after offset which represents meaningful data for the stream. Note: this value only affects reading. When writing, this length
        /// may be exceeded.
        /// </param>
        /// <param name="canGrow">True to allow the underlying data array to be automatically replaced when additional capacity is needed.</param>
        public void ReplaceData(byte[] data, int offset, int length, bool canGrow = false)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (offset < 0 || offset > data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0 || offset + length > data.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            CanGrow = canGrow;
            Data = data;
            Offset = offset;
            _realPosition = offset;
            _endPosition = offset + length;
        }

        /// <summary>
        /// Resets the length and position of the stream, effectively clearing out any existing data, and making it reusable.
        /// </summary>
        public void ResetForWriting()
        {
            _realPosition = Offset;
            _endPosition = Offset;
        }

        /// <summary>
        /// Resets position to zero, but does not clear data.
        /// </summary>
        public void ResetForReading()
        {
            _realPosition = Offset;
        }

        // not exposing this as a property because it's a struct and would likely cause surprising behavior to some users
        /// <summary>
        /// Gets the current default <see cref="StringSetOptions"/>. This controls interning for strings read from the stream. Any updates to these options
        /// must be applied by calling <see cref="SetDefaultStringSetOptions"/>.
        /// </summary>
        public StringSetOptions GetDefaultStringSetOptions()
        {
            return _defaultStringSetOptions;
        }

        /// <summary>
        /// Sets the default <see cref="StringSetOptions"/>, which controls interning for strings read from the stream.
        /// </summary>
        /// <param name="options"></param>
        public void SetDefaultStringSetOptions(StringSetOptions options)
        {
            _defaultStringSetOptions = options;
        }

        /// <summary>
        /// This method is a no-op because there is nothing to flush to.
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// Reads, at most, <paramref name="count"/> bytes from the stream and copies them into <paramref name="buffer"/> at index <paramref name="offset"/>.
        /// </summary>
        /// <param name="buffer">The destination to copy bytes to.</param>
        /// <param name="offset">The index in the destination buffer where the stream bytes should be copied to.</param>
        /// <param name="count">The maximum number of bytes to read from the stream. Fewer bytes will be read and copied if the stream has fewer unread bytes remaining.</param>
        /// <returns>The number of bytes actually read from the stream and copied.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var readCount = Math.Min(count, UnreadByteCount);

            if (readCount > 0)
            {
                var pos = _realPosition;
                Array.Copy(Data, pos, buffer, offset, readCount);
                _realPosition = pos +readCount;
            }

            return readCount;
        }

        /// <summary>
        /// Reads, at most, <paramref name="count"/> from the stream and copies them to the unmanaged buffer.
        /// </summary>
        /// <param name="buffer">A pointer to an unmanaged buffer. </param>
        /// <param name="count">The maximum number of bytes to copy from the stream to the buffer.</param>
        /// <returns>The number of bytes copied (will be the minimum of either count or UreadByteCount).</returns>
        public unsafe int Read(byte* buffer, int count)
        {
            var readCount = Math.Min(count, UnreadByteCount);

            if (readCount > 0)
            {
                var pos = _realPosition;

                // In my testing, 400 bytes is around where it becomes worth it to call Marshal.Copy instead of manually copying.
                if (readCount > 400)
                {
                    Marshal.Copy(Data, pos, (IntPtr)buffer, readCount);
                }
                else
                {
                    fixed (byte* dataPtr = Data)
                    {
                        Unsafe.MemoryCopy(&dataPtr[pos], buffer, readCount);
                    }
                }

                _realPosition = pos + readCount;
            }

            return readCount;
        }

        /// <summary>
        /// Reads <paramref name="count"/> bytes from <paramref name="buffer"/> starting at index <paramref name="offset"/> and writes them to the stream.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The index into buffer where to start reading from.</param>
        /// <param name="count">The number of bytes to write to the stream.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            int pos, newPos;
            EnsureRoomFor(count, out pos, out newPos);

            Array.Copy(buffer, offset, Data, pos, count);

            UpdateWritePosition(newPos);
        }

        /// <summary>
        /// Reads <paramref name="count"/> bytes from <paramref name="buffer"/> and writes them to the stream.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="count">The number of bytes to write to the stream.</param>
        public unsafe void Write(byte* buffer, int count)
        {
            int pos, newPos;
            EnsureRoomFor(count, out pos, out newPos);

            // In my testing, 400 bytes is around where it becomes worth it to call Marshal.Copy instead of manually copying.
            if (count > 400)
            {
                Marshal.Copy((IntPtr)buffer, Data, pos, count);
            }
            else
            {
                fixed (byte* dataPtr = Data)
                {
                    Unsafe.MemoryCopy(buffer, &dataPtr[pos], count);
                }
            }

            UpdateWritePosition(newPos);
        }

        /// <summary>
        /// Writes a single byte to the stream.
        /// </summary>
        public override void WriteByte(byte value)
        {
            int pos, newPos;
            EnsureRoomFor(1, out pos, out newPos);

            Data[pos] = value;

            UpdateWritePosition(newPos);
        }

        /// <summary>
        /// Reads one byte from the stream and returns it. If there are no bytes remaining to read, -1 is returned. This behavior is as per the .NET Stream
        /// documentation. If this is not the behavior you're looking for, consider using <see cref="ReadUInt8"/> instead.
        /// </summary>
        public override int ReadByte()
        {
            if (UnreadByteCount < 1)
                return -1; // as per the Stream documentation

            var pos = _realPosition;
            var value = Data[pos];
            _realPosition = pos + 1;

            return value;
        }

        byte ReadOneByte()
        {
            if (UnreadByteCount < 1)
                throw new IndexOutOfRangeException();

            var pos = _realPosition;
            var value = Data[pos];
            _realPosition = pos + 1;

            return value;
        }

        /// <summary>
        /// Writes two bytes from <paramref name="src"/> to the stream.
        /// </summary>
        public unsafe void WriteTwoBytes(byte* src)
        {
            int pos, newPos;
            EnsureRoomFor(2, out pos, out newPos);

            var data = Data;
            data[pos + 0] = src[0];
            data[pos + 1] = src[1];

            UpdateWritePosition(newPos);
        }

        /// <summary>
        /// Writes four bytes from <paramref name="src"/> to the stream.
        /// </summary>
        public unsafe void WriteFourBytes(byte* src)
        {
            int pos, newPos;
            EnsureRoomFor(4, out pos, out newPos);

            var data = Data;
            data[pos + 0] = src[0];
            data[pos + 1] = src[1];
            data[pos + 2] = src[2];
            data[pos + 3] = src[3];

            UpdateWritePosition(newPos);
        }

        /// <summary>
        /// Writes eight bytes from <paramref name="src"/> to the stream.
        /// </summary>
        public unsafe void WriteEightBytes(byte* src)
        {
            int pos, newPos;
            EnsureRoomFor(8, out pos, out newPos);

            var data = Data;
            data[pos + 0] = src[0];
            data[pos + 1] = src[1];
            data[pos + 2] = src[2];
            data[pos + 3] = src[3];
            data[pos + 4] = src[4];
            data[pos + 5] = src[5];
            data[pos + 6] = src[6];
            data[pos + 7] = src[7];

            UpdateWritePosition(newPos);
        }

        /// <summary>
        /// Reads two bytes from the stream and copies them to <paramref name="dest"/>.
        /// </summary>
        public unsafe void ReadTwoBytes(byte* dest)
        {
            if (UnreadByteCount < 2)
                throw new IndexOutOfRangeException();

            var pos = _realPosition;
            var data = Data;

            dest[0] = data[pos + 0];
            dest[1] = data[pos + 1];

            _realPosition = pos + 2;
        }

        /// <summary>
        /// Reads four bytes from the stream and copies them to <paramref name="dest"/>.
        /// </summary>
        public unsafe void ReadFourBytes(byte* dest)
        {
            if (UnreadByteCount < 4)
                throw new IndexOutOfRangeException();

            var pos = _realPosition;
            var data = Data;

            dest[0] = data[pos + 0];
            dest[1] = data[pos + 1];
            dest[2] = data[pos + 2];
            dest[3] = data[pos + 3];

            _realPosition = pos + 4;
        }

        /// <summary>
        /// Reads eight bytes from the stream and copies them to <paramref name="dest"/>.
        /// </summary>
        public unsafe void ReadEightBytes(byte* dest)
        {
            if (UnreadByteCount < 8)
                throw new IndexOutOfRangeException();

            var pos = _realPosition;
            var data = Data;

            dest[0] = data[pos + 0];
            dest[1] = data[pos + 1];
            dest[2] = data[pos + 2];
            dest[3] = data[pos + 3];
            dest[4] = data[pos + 4];
            dest[5] = data[pos + 5];
            dest[6] = data[pos + 6];
            dest[7] = data[pos + 7];

            _realPosition = pos + 8;
        }

        /// <summary>
        /// Writes an unsigned integer 7-bits at a time. Requires between one and ten (inclusive) bytes depending on how large the integer is.
        /// </summary>
        public void WriteVarUInt(ulong value)
        {
            var byteCount = BytesRequiredForVarInt(value);

            int pos, newPos;
            EnsureRoomFor(byteCount, out pos, out newPos);
            WriteVarIntImpl(value, byteCount, pos);

            UpdateWritePosition(newPos);
        }

        /// <summary>
        /// Reads unsigned integers which were written using WriteVarUInt().
        /// </summary>
        public ulong ReadVarUInt()
        {
            var data = Data;
            var pos = _realPosition;

            var lastByte = data[pos];
            var value = (ulong)(lastByte & 0x7F);
            pos++;

            var shift = 0;
            while ((lastByte & 0x80) != 0)
            {
                lastByte = data[pos];
                pos++;

                shift += 7;
                value |= (ulong)(lastByte & 0x7F) << shift;
            }

            // check if we read too far
            if (pos > _endPosition)
                throw new IndexOutOfRangeException();

            _realPosition = pos;

            return value;
        }

        /// <summary>
        /// Sets the current index of the stream. This will not necessarily correspond with the index of the underlying data.
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
            }

            return Position;
        }

        /// <summary>
        /// Sets the length of the stream. Any data in the underlying data array is not altered. If there is not enough capacity in the underlying array and
        /// CanGrow is false, an exception will be thrown.
        /// </summary>
        public override void SetLength(long value)
        {
            if (value < 0 || value > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value));

            var newEnd = Offset + (int)value;

            if (newEnd < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (newEnd > Data.Length)
                Grow(newEnd);

            _endPosition = newEnd;

            if (_realPosition > newEnd)
                _realPosition = newEnd;
        }

        /// <summary>
        /// Checks if there is enough room to write <paramref name="additionalBytes"/>. If not, it calls Grow(), which will throw an exception if CanGrow is false.
        /// </summary>
        void EnsureRoomFor(int additionalBytes, out int currentPos, out int newPos)
        {
            currentPos = _realPosition;
            newPos = currentPos + additionalBytes;

            if (newPos < 0)
                throw new OverflowException();

            if (newPos > Data.Length)
                Grow(newPos);
        }

        byte[] Grow(int minimumSize)
        {
            if (!CanGrow)
                throw new IndexOutOfRangeException($"ReusableStream cannot grow to the required size ({minimumSize}) because CanGrow is false");

            if (minimumSize < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumSize));

            var oldData = Data;
            var oldSize = oldData.Length;

            var newSize = oldSize;
            do
            {
                newSize *= 2;

            } while (newSize < minimumSize && newSize > oldSize);

            if (newSize < minimumSize) // could happen if there is an overflow
                newSize = minimumSize;

            var newData = new byte[newSize];
            Array.Copy(oldData, newData, oldSize);
            Data = newData;

            return newData;
        }
        
        void UpdateWritePosition(int newPos)
        {
            _realPosition = newPos;

            if (newPos > _endPosition)
                _endPosition = newPos;
        }

        /// <summary>
        /// Writes the VarUInt at the real index specificed by <paramref name="pos"/>. Does not modify _realPosition.
        /// </summary>
        /// <param name="value">The value of the integer being written.</param>
        /// <param name="byteCount">The number of bytes to write. This should be pre-calculated using BytesRequiredForVarInt().</param>
        /// <param name="pos">The real position to begin writing the value.</param>
        void WriteVarIntImpl(ulong value, int byteCount, int pos)
        {
            var data = Data;

            var endMinusOne = pos + byteCount - 1;
            while (pos < endMinusOne)
            {
                data[pos] = (byte)((value & 0x7F) | 0x80);
                value >>= 7;
                pos++;
            }

            // the last byte is just what's left in value
            data[pos] = (byte)value;
        }

        static int BytesRequiredForVarInt(ulong value)
        {
            // Binary search would be O(log n) instead of O(n) for linear search, but most values you would want to write with a VarInt
            // are integers which represent a count of something, and those are going to tend to be low-value integers, so linear should
            // be better for the common case.

            if ((value & ~0x7FUL) == 0)
                return 1;
            if ((value & ~0x3FFFUL) == 0)
                return 2;
            if ((value & ~0x1FFFFFUL) == 0)
                return 3;
            if ((value & ~0xFFFFFFFUL) == 0)
                return 4;
            if ((value & ~0x7FFFFFFFFUL) == 0)
                return 5;
            if ((value & ~0x3FFFFFFFFFFUL) == 0)
                return 6;
            if ((value & ~0x1FFFFFFFFFFFFUL) == 0)
                return 7;
            if ((value & ~0xFFFFFFFFFFFFFFUL) == 0)
                return 8;
            if ((value & ~0x7FFFFFFFFFFFFFFFUL) == 0)
                return 9;

            return 10;
        }
    }
}