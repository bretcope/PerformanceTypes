using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PerformanceTypes
{
    public partial class ReusableStream : Stream
    {
        int _realPosition; // offset + position
        int _endPosition;  // offset + length used

        public byte[] Data { get; private set; }
        public bool CanGrow { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;

        public long Capacity => Data.LongLength - Offset;
        public override long Length => _endPosition - Offset;
        public int Offset { get; private set; }
        public int UnreadByteCount => _endPosition - _realPosition;

        /// <summary>
        /// Gets or sets the encoding used for reading and writing strings.
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.UTF8;
        /// <summary>
        /// A StringSet which can be used as a string intern pool if UseStringSet is true.
        /// </summary>
        public StringSet StringSet { get; set; }
        /// <summary>
        /// True to use the StringSet as a string intern pool. Any strings which exist in the pool will not require an allocation when they are read.
        /// </summary>
        public bool UseStringSet { get; set; }
        /// <summary>
        /// True if strings should automatically be added to the StringSet intern pool.
        /// WARNING: Never set this to true if you will be reading strings from user-input, as this could cause unbounded memory growth.
        /// </summary>
        public bool AcceptDangerOfAutoInterningStrings { get; set; }

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
        /// <param name="length">The number of bytes after offset which represents meaningful data for the stream.</param>
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
        /// <param name="length">The number of bytes after offset which represents meaningful data for the stream.</param>
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

        /// <summary>
        /// This method is a no-op because there is nothing to flush to.
        /// </summary>
        public override void Flush()
        {
        }

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
        /// Copies, at most, <paramref name="count"/> from the stream to the unmanaged buffer.
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
                        UnsafeMemoryCopy(dataPtr, buffer, readCount);
                    }
                }

                _realPosition = pos + readCount;
            }

            return readCount;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int pos, newPos;
            EnsureRoomFor(count, out pos, out newPos);

            Array.Copy(buffer, offset, Data, pos, count);

            UpdateWritePosition(newPos);
        }

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
                    UnsafeMemoryCopy(buffer, dataPtr, count);
                }
            }

            UpdateWritePosition(newPos);
        }

        public override void WriteByte(byte value)
        {
            int pos, newPos;
            EnsureRoomFor(1, out pos, out newPos);

            Data[pos] = value;

            UpdateWritePosition(newPos);
        }

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

        public unsafe void WriteTwoBytes(byte* src)
        {
            int pos, newPos;
            EnsureRoomFor(2, out pos, out newPos);

            var data = Data;
            data[pos + 0] = src[0];
            data[pos + 1] = src[1];

            UpdateWritePosition(newPos);
        }

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

        public void WriteVarUInt(ulong value)
        {
            var bytes = BytesRequiredForVarInt(value);

            int pos, newPos;
            EnsureRoomFor(bytes, out pos, out newPos);
            WriteVarIntImpl(value, bytes, pos);

            UpdateWritePosition(newPos);
        }

        public ulong ReadVarUInt()
        {
            var pos = _realPosition;
            int newPos;
            var value = ReadVarIntImpl(pos, out newPos);
            _realPosition = newPos;

            return value;
        }

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
        /// <param name="value"></param>
        public override void SetLength(long value)
        {
            if (value < 0 || value > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value));

            var newEnd = Offset + (int)value;

            if (newEnd < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (newEnd > Data.Length)
                Grow(newEnd);

            _endPosition = (int)newEnd;

            throw new NotImplementedException();
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

        void Grow(int minimumSize)
        {
            if (!CanGrow)
                throw new InvalidOperationException($"ReusableStream cannot grow to the required size ({minimumSize}) because CanGrow is false");

            throw new NotImplementedException();
        }
        
        void UpdateWritePosition(int newPos)
        {
            _realPosition = newPos;

            if (newPos > _endPosition)
                _endPosition = newPos;
        }

        void WriteVarIntImpl(ulong value, int bytes, int pos)
        {
            //
            while (bytes > 0)
            {
                //
                bytes--;
            }
        }

        ulong ReadVarIntImpl(int pos, out int newPos)
        {
            throw new NotImplementedException();
        }

        static unsafe void UnsafeMemoryCopy(byte* src, byte* dest, int count)
        {
            var remainder = count;

            if (count > 8)
            {
                var longs = count / sizeof(long);
                remainder = count % sizeof(long);

                var srcLong = (long*)src;
                var destLong = (long*)dest;

                while (longs > 0)
                {
                    *destLong = *srcLong;
                    srcLong++;
                    destLong++;
                    longs--;
                }

                src = (byte*)srcLong;
                dest = (byte*)destLong;
            }

            switch (remainder)
            {
                case 1:
                    *dest = *src;
                    return;
                case 2:
                    *(short*)dest = *(short*)src;
                    return;
                case 3:
                    *(short*)dest = *(short*)src;
                    *(dest + 2) = *(src + 2);
                    return;
                case 4:
                    *(int*)dest = *(int*)src;
                    return;
                case 5:
                    *(int*)dest = *(int*)src;
                    dest[4] = src[4];
                    return;
                case 6:
                    *(int*)dest = *(int*)src;
                    *(short*)(dest + 4) = *(short*)(src + 4);
                    return;
                case 7:
                    *(int*)dest = *(int*)src;
                    *(short*)(dest + 4) = *(short*)(src + 4);
                    dest[6] = src[6];
                    return;
                case 8:
                    *(long*)dest = *(long*)src;
                    return;
            }
        }
    }
}