using System;

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

        public void WriteString(char[] chars, int offset, int length, bool nullable)
        {
            throw new NotImplementedException();
        }

        public unsafe void WriteString(char* chars, int length, bool nullable)
        {
            if (chars == null)
            {
                if (!nullable)
                    throw new ArgumentNullException(nameof(chars));

                throw new NotImplementedException();
                return;
            }

            throw new NotImplementedException();
        }

        static int BytesRequiredForVarInt(ulong value)
        {
            // could maybe do a binary search instead of linear, but most will probably be low integers

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
