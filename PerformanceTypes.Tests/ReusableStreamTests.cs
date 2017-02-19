using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace PerformanceTypes.Tests
{
    [TestFixture]
    public class ReusableStreamTests
    {
        [Test]
        public void Resetting()
        {
            var s = new ReusableStream(16);

            Assert.AreEqual(0, s.Position);
            Assert.AreEqual(0, s.Length);

            var written = long.MaxValue;
            s.Write(written);

            Assert.AreEqual(8, s.Position);
            Assert.AreEqual(8, s.Length);

            s.ResetForReading();

            Assert.AreEqual(0, s.Position);
            Assert.AreEqual(8, s.Length);

            var read = s.ReadInt64();

            Assert.AreEqual(written, read);
            Assert.AreEqual(8, s.Position);
            Assert.AreEqual(8, s.Length);
            
            s.ResetForWriting();

            Assert.AreEqual(0, s.Position);
            Assert.AreEqual(0, s.Length);

            written = 1;
            s.Write(written);

            Assert.AreEqual(8, s.Position);
            Assert.AreEqual(8, s.Length);

            s.ResetForReading();

            Assert.AreEqual(0, s.Position);
            Assert.AreEqual(8, s.Length);

            read = s.ReadInt64();

            Assert.AreEqual(written, read);
            Assert.AreEqual(8, s.Position);
            Assert.AreEqual(8, s.Length);
        }

        [Test]
        public void ReadWritePrimitives()
        {
            var s = new ReusableStream(100);
            var rng = new Random();

            var b = (byte)rng.Next(255);
            var sb = (sbyte)rng.Next(255);
            var sh = (short)rng.Next(short.MinValue, short.MaxValue);
            var ush = (ushort)rng.Next(ushort.MaxValue);
            var i = rng.Next(int.MinValue, int.MaxValue);
            var ui = (uint)rng.Next(int.MinValue, int.MaxValue);
            var l = (long)RandomULong(rng);
            var ul = RandomULong(rng);
            var f = (float)rng.NextDouble();
            var d = rng.NextDouble();
            var c = (char)rng.Next(char.MinValue, char.MaxValue);
            var t = DateTime.UtcNow;
            var g = Guid.NewGuid();

            var expectedLength = 0;

            s.Write(b);
            expectedLength += 1;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(sb);
            expectedLength += 1;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(sh);
            expectedLength += 2;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(ush);
            expectedLength += 2;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(i);
            expectedLength += 4;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(ui);
            expectedLength += 4;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(l);
            expectedLength += 8;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(ul);
            expectedLength += 8;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(f);
            expectedLength += 4;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(d);
            expectedLength += 8;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(c);
            expectedLength += 2;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(false);
            expectedLength += 1;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(true);
            expectedLength += 1;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(t);
            expectedLength += 8;
            Assert.AreEqual(expectedLength, s.Length);
            s.Write(g);
            expectedLength += 16;
            Assert.AreEqual(expectedLength, s.Length);

            s.ResetForReading();

            Assert.AreEqual(b, s.ReadUInt8());
            Assert.AreEqual(sb, s.ReadInt8());
            Assert.AreEqual(sh, s.ReadInt16());
            Assert.AreEqual(ush, s.ReadUInt16());
            Assert.AreEqual(i, s.ReadInt32());
            Assert.AreEqual(ui, s.ReadUInt32());
            Assert.AreEqual(l, s.ReadInt64());
            Assert.AreEqual(ul, s.ReadUInt64());
            Assert.AreEqual(f, s.ReadSingle());
            Assert.AreEqual(d, s.ReadDouble());
            Assert.AreEqual(c, s.ReadChar());
            Assert.AreEqual(false, s.ReadBoolean());
            Assert.AreEqual(true, s.ReadBoolean());
            Assert.AreEqual(t, s.ReadDateTime());
            Assert.AreEqual(g, s.ReadGuid());

            // verify that we read to the end
            Assert.AreEqual(s.Length, s.Position);

            s.ResetForReading();
            Assert.AreEqual((int)b, s.ReadByte());
        }

        static ulong RandomULong(Random rng)
        {
            var low = (ulong)rng.Next(int.MinValue, int.MaxValue);
            var high = (ulong)rng.Next(int.MinValue, int.MaxValue);

            return (high << 32) | low;
        }

        [Test]
        public void CanGrow()
        {
            var s = new ReusableStream(8, true);

            Assert.AreEqual(8, s.Capacity);
            s.Write((long)3);

            // there shouldn't be any change yet
            Assert.AreEqual(8, s.Capacity);

            s.Write(4);
            Assert.IsTrue(s.Capacity >= 12);
        }

        [Test]
        public void CanGrowDisabled()
        {
            var s = new ReusableStream(8, false);

            Assert.AreEqual(8, s.Capacity);
            s.Write((long)3);

            // there shouldn't be any change
            Assert.AreEqual(8, s.Capacity);

            Assert.Throws<IndexOutOfRangeException>(() => s.Write(4));
            
            // there shouldn't be any change
            Assert.AreEqual(8, s.Capacity);
        }

        [Test]
        public void UninitializedStream()
        {
            var s = new ReusableStream();

            Assert.Throws<NullReferenceException>(() => s.Write(true));
            Assert.Throws<IndexOutOfRangeException>(() => s.ReadBoolean());
        }

        [Test]
        public void BadInitializationParameters()
        {
            Assert.Throws<ArgumentNullException>(() => new ReusableStream(null, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReusableStream(new byte[2], -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReusableStream(new byte[2], 3, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReusableStream(new byte[2], 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReusableStream(new byte[2], 0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReusableStream(new byte[2], 1, 2));
        }

        [Test]
        public unsafe void ReadWriteBytes()
        {
            var s = new ReusableStream(4000);
            var rng = new Random();

            var tests = new byte[][]
            {
                new byte[1],
                new byte[2],
                new byte[3],
                new byte[4],
                new byte[5],
                new byte[6],
                new byte[7],
                new byte[8],
                new byte[9],
                new byte[10],
                new byte[11],
                new byte[12],
                new byte[13],
                new byte[14],
                new byte[15],
                new byte[16],
                new byte[17],
                new byte[127],
                new byte[128],
                new byte[129],
                new byte[255],
                new byte[256],
                new byte[257],
                new byte[400],
                new byte[800],
                new byte[1200],
            };

            var expectedLength = 0;

            foreach (var test in tests)
            {
                rng.NextBytes(test);
                s.Write(test);
                expectedLength += test.Length;
                Assert.AreEqual(expectedLength, s.Length);
            }

            s.ResetForReading();

            foreach (var test in tests)
            {
                var read = new byte[test.Length];
                s.Read(read, 0, read.Length);
                AssertBytesAreEqual(test, read);
            }

            // do the same thing using the unsafe methods now

            s = new ReusableStream(4000);

            expectedLength = 0;

            foreach (var test in tests)
            {
                fixed (byte* ptr = test)
                {
                    s.Write(ptr, test.Length);
                }
                expectedLength += test.Length;
                Assert.AreEqual(expectedLength, s.Length);
            }

            s.ResetForReading();

            foreach (var test in tests)
            {
                var read = new byte[test.Length + 16];
                fixed (byte* ptr = read)
                {
                    const ulong GUARD = 0xAAAAAAAAAAAAAAAA;

                    var startGuard = (ulong*)ptr;
                    var data = &ptr[8];
                    var endGuard = (ulong*)&data[test.Length];

                    *startGuard = GUARD;
                    *endGuard = GUARD;

                    s.Read(data, test.Length);

                    Assert.AreEqual(GUARD, *startGuard);
                    Assert.AreEqual(GUARD, *endGuard);
                }

                var actual = new byte[test.Length];
                Array.Copy(read, 8, actual, 0, test.Length);

                AssertBytesAreEqual(test, actual);
            }
        }

        [Test]
        public unsafe void ReadPastEnd()
        {
            var s = new ReusableStream(16);

            s.Write((ulong)3);

            var buffer = new byte[16];
            fixed (byte* p = buffer)
            {
                var bufferPtr = p;

                Assert.AreEqual(-1, s.ReadByte());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadBoolean());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadInt8());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadUInt8());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadInt16());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadUInt16());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadInt32());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadUInt32());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadInt64());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadUInt64());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadSingle());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadDouble());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadDateTime());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadGuid());
                Assert.Throws<IndexOutOfRangeException>(() => s.ReadString(true));

                Assert.AreEqual(0, s.Read(buffer, 0, 1));
                Assert.AreEqual(0, s.Read(bufferPtr, 1));

                s.ResetForReading();
                Assert.AreEqual(8, s.Read(buffer, 0, 9));

                s.ResetForReading();
                Assert.AreEqual(8, s.Read(bufferPtr, 9));
            }
        }

        [Test]
        public void Seek()
        {
            var data = new byte[16];
            var rng = new Random();
            rng.NextBytes(data);
            const int offset = 2;
            var s = new ReusableStream(data, offset, data.Length - offset);

            Assert.AreEqual(data[offset], s.ReadUInt8());

            s.Seek(0, SeekOrigin.Begin);
            Assert.AreEqual(data[offset], s.ReadUInt8());

            s.Seek(2, SeekOrigin.Current);
            Assert.AreEqual(data[offset + 3], s.ReadUInt8());

            s.Seek(-1, SeekOrigin.Current);
            Assert.AreEqual(data[offset + 3], s.ReadUInt8());

            s.Seek(-1, SeekOrigin.End);
            Assert.AreEqual(data[data.Length - 1], s.ReadUInt8());
        }

        [Test]
        public void ReplacingData()
        {
            var rng = new Random();
            var one = new byte[16];
            var two = new byte[32];
            rng.NextBytes(one);
            rng.NextBytes(two);

            var readable = one.Length - 2;
            var s = new ReusableStream(one, 0, readable);

            Assert.AreEqual(readable, s.Length);
            Assert.AreEqual(readable, s.UnreadByteCount);
            Assert.AreEqual(0, s.Offset);
            Assert.AreEqual(0, s.Position);

            var readCount = 0;
            while (s.UnreadByteCount > 0)
            {
                Assert.AreEqual(one[readCount], s.ReadUInt8());
                readCount++;
            }

            Assert.AreEqual(readable, readCount);

            readable = two.Length - 2;
            s.ReplaceData(two, 2, readable);

            Assert.AreEqual(readable, s.Length);
            Assert.AreEqual(readable, s.UnreadByteCount);
            Assert.AreEqual(2, s.Offset);
            Assert.AreEqual(0, s.Position);

            readCount = 0;
            while (s.UnreadByteCount > 0)
            {
                Assert.AreEqual(two[2 + readCount], s.ReadUInt8());
                readCount++;
            }

            Assert.AreEqual(readable, readCount);
        }

        [Test]
        public void SetLength()
        {
            var s = new ReusableStream(16);

            Assert.AreEqual(0, s.Length);
            Assert.AreEqual(0, s.UnreadByteCount);
            Assert.AreEqual(16, s.Capacity);

            s.SetLength(4);
            Assert.AreEqual(4, s.Length);
            Assert.AreEqual(4, s.UnreadByteCount);
            Assert.AreEqual(16, s.Capacity);

            s.ReadInt32();
            Assert.AreEqual(4, s.Length);
            Assert.AreEqual(0, s.UnreadByteCount);
            Assert.AreEqual(16, s.Capacity);

            s.SetLength(16);
            Assert.AreEqual(16, s.Length);
            Assert.AreEqual(12, s.UnreadByteCount);
            Assert.AreEqual(16, s.Capacity);

            s.SetLength(17);
            Assert.AreEqual(17, s.Length);
            Assert.AreEqual(13, s.UnreadByteCount);
            Assert.IsTrue(s.Capacity >= 17);

            s.SetLength(0);
            Assert.AreEqual(0, s.Length);
            Assert.AreEqual(0, s.UnreadByteCount);
            Assert.IsTrue(s.Capacity >= 17);

            Assert.Throws<ArgumentOutOfRangeException>(() => s.SetLength(-1));
        }

        [Test]
        public void VarInts()
        {
            var s = new ReusableStream(1000);

            var ints = new ulong[]
            {
                17,
                23,
                0,
                1,
                2,
                127,
                128,
                255,
                256,
                (ulong)short.MaxValue,
                ushort.MaxValue,
                int.MaxValue,
                uint.MaxValue,
                long.MaxValue,
                ulong.MaxValue,
                0x7FUL,
                0x7FUL + 1,
                0x3FFFUL,
                0x3FFFUL + 1,
                0x1FFFFFUL,
                0x1FFFFFUL + 1,
                0xFFFFFFFUL,
                0xFFFFFFFUL + 1,
                0x7FFFFFFFFUL,
                0x7FFFFFFFFUL + 1,
                0x3FFFFFFFFFFUL,
                0x3FFFFFFFFFFUL + 1,
                0x1FFFFFFFFFFFFUL,
                0x1FFFFFFFFFFFFUL + 1,
                0xFFFFFFFFFFFFFFUL,
                0xFFFFFFFFFFFFFFUL + 1,
                0x7FFFFFFFFFFFFFFFUL,
                0x7FFFFFFFFFFFFFFFUL + 1,
            };

            foreach (var ui in ints)
            {
                s.WriteVarUInt(ui);
            }

            s.ResetForReading();

            foreach (var ui in ints)
            {
                var read = s.ReadVarUInt();
                Assert.AreEqual(ui, read);
            }
        }

        [Test]
        public void StringEncodingLengths()
        {
            var s = new ReusableStream(100);

            var expectedLength = 0;

            var strings = new[]
            {
                "a",
                "瀬",
                "𐐷",
            };

            var encodings = new[]
            {
                Encoding.ASCII,
                Encoding.UTF7,
                Encoding.UTF8,
                Encoding.Unicode,
                Encoding.BigEndianUnicode,
                Encoding.UTF32,
            };

            foreach (var str in strings)
            {
                foreach (var e in encodings)
                {
                    s.WriteString(str, false, e);
                    expectedLength += e.GetByteCount(str) + 1;
                    Assert.AreEqual(expectedLength, s.Length);
                }
            }

            s.ResetForReading();

            foreach (var str in strings)
            {
                foreach (var e in encodings)
                {
                    var read = s.ReadString(false, e);

                    if (e == Encoding.ASCII)
                        continue;

                    Assert.AreEqual(str, read);
                }
            }
        }

        [Test]
        public unsafe void Strings()
        {
            // not including ASCII since it won't round-trip for the unicode strings
            var encodings = new[]
            {
                Encoding.UTF7,
                Encoding.UTF8,
                Encoding.Unicode,
                Encoding.BigEndianUnicode,
                Encoding.UTF32,
            };

            var strings = new[]
            {
                "",
                "a",
                "cat",
                @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nunc odio ligula, pharetra eget eros et, blandit luctus justo. Fusce ultricies id tortor sit amet laoreet. Cras posuere tellus vel aliquam tristique. Duis et quam sit amet sapien ullamcorper blandit a sed dolor. Duis placerat nisl ac egestas suscipit. Sed lacus tellus, convallis placerat sodales vitae, tempor et urna. Duis ut leo dictum, tempus ante ut, lacinia nisi. Nunc consectetur orci nisl, vitae fringilla justo vestibulum vitae. Proin orci velit, iaculis ut ornare quis, rhoncus in lectus.

Donec volutpat convallis faucibus. Donec finibus erat sit amet tortor rhoncus suscipit. Quisque facilisis lacus risus, sit amet pretium ipsum ornare et. Donec nec elementum dui. Nunc volutpat commodo metus ac tincidunt. Aenean eget lectus lacus. Donec mauris libero, accumsan id ex a, mollis rutrum lacus. Praesent mollis pretium ipsum eu faucibus. Fusce posuere congue libero, ut aliquam nulla dapibus vitae. Etiam sit amet convallis justo, sit amet hendrerit diam.

Suspendisse eleifend vitae quam ac tempor. Sed fringilla tempus enim in fringilla. Fusce at leo metus. Vestibulum sollicitudin tempus odio, non posuere leo congue lobortis. Maecenas aliquam diam eget urna finibus, vitae egestas ipsum ultrices. Sed risus enim, tempor eget tortor non, malesuada auctor nulla. Aliquam egestas posuere erat, quis interdum elit efficitur sed. Suspendisse malesuada, ipsum id iaculis semper, nunc tortor congue nulla, non auctor est magna vitae ligula. Nulla sit amet orci nisl. Nulla non bibendum felis, id tempor tortor.

Nam neque neque, tempus quis semper id, porttitor vitae tortor. Nulla sit amet leo eros. Orci varius natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus. Fusce varius ipsum non risus lobortis, et pretium elit malesuada. Sed luctus fermentum orci, et rutrum lacus faucibus in. Integer sodales leo aliquet mattis tristique. Nam consequat ornare lorem eget fermentum. Vestibulum a lectus sit amet nibh egestas fermentum sit amet at est. Suspendisse quis dapibus metus, sed imperdiet felis. Nulla nec consequat elit. Pellentesque pretium erat ornare felis semper tincidunt. Nullam nec magna lectus. Donec feugiat ligula urna, eget sodales ipsum fermentum sed. Morbi nec metus sit amet neque finibus hendrerit in quis ligula. Morbi lectus nulla, porta sed eros commodo, aliquam scelerisque felis.

Proin fringilla pellentesque odio. Sed finibus in dolor non laoreet. Mauris in magna eget ex faucibus varius sed nec arcu. Sed tincidunt ut nulla sit amet rutrum. Mauris at neque neque. Nulla vel urna rhoncus, ultricies quam aliquet, congue nunc. Sed lectus dolor, placerat at tempor at, lacinia non ipsum. Maecenas commodo, lectus eget blandit laoreet, massa nunc egestas leo, non iaculis felis felis at lorem. Ut ut sagittis arcu, et sollicitudin nisi. Aliquam auctor porta rhoncus. Ut est enim, dictum at sollicitudin vitae, feugiat sed urna. Pellentesque pretium mi id nunc commodo, id efficitur metus vulputate. Donec rhoncus, lectus sed tincidunt accumsan, metus odio vehicula lectus, vitae tristique tellus ex id enim.",
                "っつ雲日御へ保瀬とれろなほニミャメ",
                "𐐷",
                "END"
            };

            foreach (var encoding in encodings)
            {
                // try writing strings
                var s = new ReusableStream(10000);
                s.DefaultEncoding = encoding;

                foreach (var t in strings)
                {
                    s.WriteString(t, false);
                }

                s.ResetForReading();

                foreach (var t in strings)
                {
                    var str = s.ReadString(false);
                    Assert.AreEqual(t, str);
                }

                // try writing char[]
                s = new ReusableStream(10000);
                s.DefaultEncoding = encoding;

                foreach (var t in strings)
                {
                    s.WriteString(t.ToCharArray(), 0, t.Length, false);
                }

                s.ResetForReading();

                foreach (var t in strings)
                {
                    var str = s.ReadString(false);
                    Assert.AreEqual(t, str);
                }

                // try writing char*
                s = new ReusableStream(10000);
                s.DefaultEncoding = encoding;

                foreach (var t in strings)
                {
                    fixed (char* ptr = t)
                    {
                        s.WriteString(ptr, t.Length, false);
                    }
                }

                s.ResetForReading();

                foreach (var t in strings)
                {
                    var str = s.ReadString(false);
                    Assert.AreEqual(t, str);
                }
            }
        }

        [Test]
        public void StringEdgeCases()
        {
            // The purpose of this method is to trigger some edge cases in the string writing code where the max encoded size of given string length is greater
            // than the remaining bytes in the stream; however, the *actual* encoded string might still fit. If the real encoded size will fit, the stream
            // should not grow.

            var s = new ReusableStream(8);

            var sevenAscii = "seven c"; // should fit
            var sevenUnicode = "seven 瀬"; // won't fit

            s.WriteString(sevenAscii, false);
            Assert.AreEqual(8, s.Length);
            Assert.AreEqual(8, s.Capacity);

            s.ResetForWriting();

            s.WriteString(sevenUnicode, false);
            Assert.AreEqual(10, s.Length);
            Assert.IsTrue(s.Capacity >= 10);
        }

        [Test]
        public unsafe void StringsNullable()
        {
            var s = new ReusableStream(40);

            var expectedLength = 0;
            const string emptyString = "";

            s.WriteString(null, true);
            expectedLength += 2;
            Assert.AreEqual(expectedLength, s.Length);

            s.WriteString(null, 0, 0, true);
            expectedLength += 2;
            Assert.AreEqual(expectedLength, s.Length);

            s.WriteString(null, 0, true);
            expectedLength += 2;
            Assert.AreEqual(expectedLength, s.Length);

            s.WriteString(emptyString, true);
            expectedLength += 2;
            Assert.AreEqual(expectedLength, s.Length);

            s.WriteString(Array.Empty<char>(), 0, 0, true);
            expectedLength += 2;
            Assert.AreEqual(expectedLength, s.Length);

            fixed (char* ptr = emptyString)
                s.WriteString(ptr, 0, true);
            expectedLength += 2;
            Assert.AreEqual(expectedLength, s.Length);

            s.ResetForReading();

            Assert.AreEqual(null, s.ReadString(true));
            Assert.AreEqual(null, s.ReadString(true));
            Assert.AreEqual(null, s.ReadString(true));
            Assert.AreEqual(emptyString, s.ReadString(true));
            Assert.AreEqual(emptyString, s.ReadString(true));
            Assert.AreEqual(emptyString, s.ReadString(true));

            Assert.Throws<ArgumentNullException>(() => s.WriteString(null, false));
            Assert.Throws<ArgumentNullException>(() => s.WriteString(null, 0, 0, false));
            Assert.Throws<ArgumentNullException>(() => s.WriteString(null, 0, false));
        }

        [Test]
        public void StringInterning()
        {
            var s = new ReusableStream(100);

            var strings = new[] {"cat", "deer", "snail", "dog", "frog", "human"};
            const int max = 4;
            const string exclude = "frog";

            foreach (var str in strings)
            {
                s.WriteString(str, false);
            }

            s.ResetForReading();

            // we're not interning yet - make sure new strings were returned
            foreach (var str in strings)
            {
                var read = s.ReadString(false);
                Assert.AreEqual(str, read);
                Assert.AreNotSame(str, read);
            }

            s.ResetForReading();

            var options = new StringSetOptions();
            options.MaxEncodedSizeToLookupInSet = max;

            s.SetDefaultStringSetOptions(options);

            // should throw because no StringSet has been provided
            Assert.Throws<InvalidOperationException>(() => s.ReadString(false));

            var set = new StringSet(10);
            foreach (var str in strings)
            {
                if (str != exclude)
                    set.Add(str);
            }

            s.StringSet = set;

            // read with interning (but no auto-interning)
            foreach (var str in strings)
            {
                var read = s.ReadString(false);
                Assert.AreEqual(str, read);

                if (str.Length <= max && str != exclude)
                    Assert.AreSame(str, read);
                else
                    Assert.AreNotSame(str, read);
            }

            // make sure the excluded string didn't get added to the set
            Assert.AreEqual(null, set.GetExistingString(exclude.ToCharArray(), 0, exclude.Length));

            s.ResetForReading();
            options.PerformDangerousAutoAddToSet = true;
            s.SetDefaultStringSetOptions(options);

            // read with auto-interning
            foreach (var str in strings)
            {
                var read = s.ReadString(false);
                Assert.AreEqual(str, read);

                if (str.Length <= max && str != exclude)
                    Assert.AreSame(str, read);
                else
                    Assert.AreNotSame(str, read);
            }

            // make sure the excluded string got added to the set
            Assert.AreEqual(exclude, set.GetExistingString(exclude.ToCharArray(), 0, exclude.Length));
        }

        static void AssertBytesAreEqual(byte[] expected, byte[] actual)
        {
            if (expected.Length != actual.Length)
                throw BytesNotEqual(actual, expected);

            for (var i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                    throw BytesNotEqual(expected, actual);
            }
        }

        static Exception BytesNotEqual(byte[] expected, byte[] actual)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Byte arrays are not equal.");
            sb.AppendLine();
            sb.Append("Expected: ");
            WriteHexBytes(expected, sb);
            sb.AppendLine();
            sb.Append("Actual:   ");
            WriteHexBytes(actual, sb);
            sb.AppendLine();

            return new Exception(sb.ToString());
        }

        static void WriteHexBytes(byte[] bytes, StringBuilder sb)
        {
            foreach (var b in bytes)
            {
                sb.Append(Convert.ToString(b, 16).PadLeft(2, '0'));
                sb.Append(' ');
            }
        }
    }
}