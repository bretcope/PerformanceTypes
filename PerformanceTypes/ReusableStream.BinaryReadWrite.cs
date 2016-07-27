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

        public unsafe string ReadString(bool nullable, Encoding encoding = null, StringSetOptions? setOptions = null)
        {
            var encodedSize = (int)ReadVarUInt();

            if (encodedSize == 0)
            {
                return nullable && ReadBoolean() ? null : "";
            }

            var pos = _realPosition;
            var data = Data;
            
            if (pos + encodedSize > data.Length)
                throw new IndexOutOfRangeException();

            if (encoding == null)
                encoding = DefaultEncoding;

            var options = setOptions ?? _defaultStringSetOptions;

            if (encodedSize > options.MaxEncodedSizeToLookupInSet)
            {
                // don't care about interning, just read the string
                var str = encoding.GetString(Data, pos, encodedSize);

                _realPosition = pos + encodedSize;
                return str;
            }

            // we're going to use the StringSet as an intern pool
            var set = StringSet;
            if (set == null)
                throw new InvalidOperationException("StringSetOptions.MaxEncodedSizeToLookupInSet is greater than zero, but StringSet is null.");

            var maxChars = encoding.GetMaxCharCount(encodedSize);
            var charBuffer = stackalloc char[maxChars];

            int charsWritten;
            fixed (byte* dataPtr = data)
            {
                charsWritten = encoding.GetChars(dataPtr, encodedSize, charBuffer, maxChars);
            }

            // now that we've got the characters in a buffer, calculate the hash and see if it exists in the set
            if (options.PerformDangerousAutoAddToSet)
            {
                string str;
                set.Add(charBuffer, charsWritten, out str);
                return str;
            }
            else
            {
                var str = set.GetExistingString(charBuffer, charsWritten);
                return str ?? new string(charBuffer, 0, charsWritten);
            }
        }

        public void WriteString(string s, bool nullable, Encoding encoding = null)
        {
            if (s == null)
            {
                if (!nullable)
                    throw new ArgumentNullException(nameof(s));

                WriteNullString();
                return;
            }

            if (s.Length == 0)
            {
                WriteZeroLengthString(nullable);
                return;
            }

            if (encoding == null)
                encoding = DefaultEncoding;

            // we actually have some characters to write
            var info = PrepareForStringWrite(s.Length, encoding);

            var data = Data;
            if (info.NeedsExactCount)
            {
                var realSize = encoding.GetByteCount(s);
                if (realSize > data.Length - info.StringPos)
                {
                    data = Grow(info.StringPos + realSize);
                }
            }

            // if we've gotten here, then the data buffer is large enough to hold the string. Time to perform the write.
            var bytesWritten = encoding.GetBytes(s, 0, s.Length, data, info.StringPos);
            WriteVarIntImpl((ulong)bytesWritten, info.VarIntByteCount, info.VarIntPos);

            UpdateWritePosition(info.StringPos + bytesWritten);
        }

        public void WriteString(char[] chars, int offset, int count, bool nullable, Encoding encoding = null)
        {
            if (chars == null)
            {
                if (!nullable)
                    throw new ArgumentNullException(nameof(chars));

                if (count != 0)
                    throw new ArgumentOutOfRangeException(nameof(count), "Count must equal zero to write a null string.");

                WriteNullString();
                return;
            }

            if (offset < 0 || offset > chars.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0 || offset + count > chars.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0)
            {
                WriteZeroLengthString(nullable);
                return;
            }

            if (encoding == null)
                encoding = DefaultEncoding;

            // we actually have some characters to write
            var info = PrepareForStringWrite(count, encoding);

            var data = Data;
            if (info.NeedsExactCount)
            {
                var realSize = encoding.GetByteCount(chars, offset, count);
                if (realSize > data.Length - info.StringPos)
                {
                    data = Grow(info.StringPos + realSize);
                }
            }

            // if we've gotten here, then the data buffer is large enough to hold the string. Time to perform the write.
            var bytesWritten = encoding.GetBytes(chars, offset, count, data, info.StringPos);
            WriteVarIntImpl((ulong)bytesWritten, info.VarIntByteCount, info.VarIntPos);

            UpdateWritePosition(info.StringPos + bytesWritten);
        }

        public unsafe void WriteString(char* chars, int count, bool nullable, Encoding encoding = null)
        {
            if (chars == null)
            {
                if (!nullable)
                    throw new ArgumentNullException(nameof(chars));

                if (count != 0)
                    throw new ArgumentOutOfRangeException(nameof(count), "Count must equal zero to write a null string.");

                WriteNullString();
                return;
            }

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0)
            {
                WriteZeroLengthString(nullable);
                return;
            }

            if (encoding == null)
                encoding = DefaultEncoding;

            // we actually have some characters to write
            var info = PrepareForStringWrite(count, encoding);

            var data = Data;
            if (info.NeedsExactCount)
            {
                var realSize = encoding.GetByteCount(chars, count);
                if (realSize > data.Length - info.StringPos)
                {
                    data = Grow(info.StringPos + realSize);
                }
            }

            // if we've gotten here, then the data buffer is large enough to hold the string. Time to perform the write.
            int bytesWritten;
            fixed (byte* dataPtr = data)
            {
                bytesWritten = encoding.GetBytes(chars, count, dataPtr, data.Length - info.StringPos);
            }

            WriteVarIntImpl((ulong)bytesWritten, info.VarIntByteCount, info.VarIntPos);

            UpdateWritePosition(info.StringPos + bytesWritten);
        }

        void WriteNullString()
        {
            int pos, newPos;
            EnsureRoomFor(2, out pos, out newPos);

            var data = Data;
            data[0] = 0; // first byte indicates zero length
            data[1] = 1; // second byte = 1 indicates null

            UpdateWritePosition(newPos);
        }

        void WriteZeroLengthString(bool nullable)
        {
            int pos, newPos;

            if (nullable)
            {
                EnsureRoomFor(2, out pos, out newPos);

                var data = Data;
                data[0] = 0;
                data[1] = 0;
            }
            else
            {
                EnsureRoomFor(1, out pos, out newPos);

                var data = Data;
                data[0] = 0;
            }

            UpdateWritePosition(newPos);
        }

        struct StringPrepInfo
        {
            public int VarIntByteCount;
            public int VarIntPos;
            public int StringPos;
            public bool NeedsExactCount;
        }

        StringPrepInfo PrepareForStringWrite(int count, Encoding encoding)
        {
            var info = default(StringPrepInfo);

            var maxSize = encoding.GetMaxByteCount(count);
            info.VarIntByteCount = BytesRequiredForVarInt((ulong)maxSize);
            info.VarIntPos = _realPosition;

            info.StringPos = info.VarIntPos + info.VarIntByteCount; // leave a spot to write the length prefix

            var data = Data;
            var remainingInBuffer = data.Length - info.StringPos;

            if (maxSize > remainingInBuffer)
            {
                // there might not be enough room in the data buffer
                var minSize = GetMinimumEncodedByteCount(encoding, count);
                if (minSize > remainingInBuffer)
                {
                    // there definitely isn't room for the string, attempt to grow to cover the max size
                    Grow(info.StringPos + maxSize);
                }
                else
                {
                    // Either we don't know what the minimum size of this encoding is, or the minimum size is small enough to fit in
                    // the data buffer. Either way, unfortunately we now have to check how big the string will actually be before we write it.
                    info.NeedsExactCount = true;
                }
            }

            return info;
        }

        static int? GetMinimumEncodedByteCount(Encoding encoding, int charCount)
        {
            if (encoding is UTF8Encoding || encoding is ASCIIEncoding || encoding is UTF7Encoding)
                return charCount;

            // in theory, if every pair of characters were surrogate pairs, UTF32 could encode to the same size as UTF16
            if (encoding is UnicodeEncoding || encoding is UTF32Encoding)
                return charCount * 2;

            // don't know anything about this encoding
            return null;
        }
    }
}
