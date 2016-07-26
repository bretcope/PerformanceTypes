using System;
using System.Text;

namespace PerformanceTypes
{
    public partial class ReusableStream
    {
        public void Write(bool value)
        {
            WriteByte((byte)(value ? 1 : 0));
        }

        public bool ReadBoolean()
        {
            return ReadUInt8() == 1;
        }

        public unsafe void Write(char value)
        {
            WriteTwoBytes((byte*)&value);
        }

        public unsafe char ReadChar()
        {
            char ch;
            ReadTwoBytes((byte*)&ch);
            return ch;
        }

        public void Write(sbyte value)
        {
            WriteByte((byte)value);
        }

        public sbyte ReadInt8()
        {
            return (sbyte)ReadOneByte();
        }

        public void Write(byte value)
        {
            WriteByte(value);
        }

        public byte ReadUInt8()
        {
            return ReadOneByte();
        }

        public unsafe void Write(short value)
        {
            WriteTwoBytes((byte*)&value);
        }

        public unsafe short ReadInt16()
        {
            short value;
            ReadTwoBytes((byte*)&value);
            return value;
        }

        public unsafe void Write(ushort value)
        {
            WriteTwoBytes((byte*)&value);
        }

        public unsafe ushort ReadUInt16()
        {
            ushort value;
            ReadTwoBytes((byte*)&value);
            return value;
        }

        public unsafe void Write(int value)
        {
            WriteFourBytes((byte*)&value);
        }

        public unsafe int ReadInt32()
        {
            int value;
            ReadFourBytes((byte*)&value);
            return value;
        }

        public unsafe void Write(uint value)
        {
            WriteFourBytes((byte*)&value);
        }

        public unsafe uint ReadUInt32()
        {
            uint value;
            ReadFourBytes((byte*)&value);
            return value;
        }

        public unsafe void Write(long value)
        {
            WriteEightBytes((byte*)&value);
        }

        public unsafe long ReadInt64()
        {
            long value;
            ReadEightBytes((byte*)&value);
            return value;
        }

        public unsafe void Write(ulong value)
        {
            WriteEightBytes((byte*)&value);
        }

        public unsafe ulong ReadUInt64()
        {
            ulong value;
            ReadEightBytes((byte*)&value);
            return value;
        }

        public unsafe void Write(float value)
        {
            WriteFourBytes((byte*)&value);
        }

        public unsafe float ReadSingle()
        {
            float value;
            ReadFourBytes((byte*)&value);
            return value;
        }

        public unsafe void Write(double value)
        {
            WriteEightBytes((byte*)&value);
        }

        public unsafe double ReadDouble()
        {
            double value;
            ReadEightBytes((byte*)&value);
            return value;
        }

        public unsafe void Write(DateTime value)
        {
            var int64 = value.ToBinary();
            WriteEightBytes((byte*)&int64);
        }

        public unsafe DateTime ReadDateTime()
        {
            long int64;
            ReadEightBytes((byte*)&int64);
            return DateTime.FromBinary(int64);
        }

        public unsafe void Write(Guid value)
        {
            Write((byte*)&value, sizeof(Guid));
        }

        public unsafe Guid ReadGuid()
        {
            Guid guid;
            Read((byte*)&guid, sizeof(Guid));
            return guid;
        }

        public string ReadString(bool nullable)
        {
            throw new NotImplementedException();
        }

        public void WriteString(string s, bool nullable)
        {
            throw new NotImplementedException();
        }

        public void WriteString(char[] chars, int offset, int count, bool nullable)
        {
            throw new NotImplementedException();
        }

        public unsafe void WriteString(char* chars, int count, bool nullable)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (chars == null)
            {
                if (!nullable)
                    throw new ArgumentNullException(nameof(chars));

                WriteByte(0); // first byte indicates zero length
                WriteByte(1); // second byte = 1 indicates null
                return;
            }

            if (count == 0)
            {
                WriteByte(0); // first byte indicates zero length

                if (nullable)
                    WriteByte(0); // second byte = 0 indicates a zero-length string rather than null
                
                return;
            }

            // we actually have some characters to write
            var encoding = Encoding;
            var maxSize = Encoding.GetMaxByteCount(count);
            var varIntByteCount = BytesRequiredForVarInt((ulong)maxSize);
            var varIntPos = _realPosition;

            var pos = varIntPos + varIntByteCount; // leave a spot to write the length prefix

            var data = Data;
            var remainingInBuffer = data.Length - pos;

            if (maxSize > remainingInBuffer)
            {
                // there might not be enough room in the data buffer
                var minSize = GetMinimumEncodedByteCount(encoding, count);
                if (minSize > remainingInBuffer)
                {
                    // there definitely isn't room for the string, attempt to grow to cover the max size
                    Grow(pos + maxSize);
                }
                else
                {
                    // Either we don't know what the minimum size of this encoding is, or the minimum size is small enough to fit in
                    // the data buffer. Either way, unfortunately we now have to check how big the string will actually be before we write it.
                    var realSize = encoding.GetByteCount(chars, count);
                    if (realSize > remainingInBuffer)
                    {
                        Grow(pos + realSize);
                    }
                }
            }

            // if we've gotten here, then the data buffer is large enough to hold the string. Time to perform the write
            int bytesWritten;
            fixed (byte* dataPtr = data)
            {
                bytesWritten = Encoding.GetBytes(chars, count, dataPtr, maxSize);
            }

            WriteVarIntImpl((ulong)bytesWritten, varIntByteCount, varIntPos);

            UpdateWritePosition(pos + bytesWritten);
        }

        static int? GetMinimumEncodedByteCount(Encoding encoding, int charCount)
        {
            if (encoding == Encoding.UTF8 || encoding == Encoding.ASCII || encoding == Encoding.UTF7)
                return charCount;

            // in theory, if every pair of characters were surrogate pairs, UTF32 could encode to the same size as UTF16
            if (encoding == Encoding.Unicode || encoding == Encoding.UTF32)
                return charCount * 2;

            // don't know anything about this encoding
            return null;
        }
    }
}
