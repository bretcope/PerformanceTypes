using System;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace PerformanceTypes.Tests
{
    [TestFixture]
    public class Md5Tests
    {
        [Test]
        public unsafe void DigestEquality()
        {
            var one = default(Md5Digest);
            var two = default(Md5Digest);

            Assert.IsTrue(one == two);
            Assert.IsFalse(one != two);

            var onePtr = (byte*)&one;
            var twoPtr = (byte*)&two;

            // make sure every byte is accounted for
            for (var i = 0; i < Md5Digest.SIZE; i++)
            {
                onePtr[i] = 1;
                Assert.IsFalse(one == two);
                Assert.IsTrue(one != two);

                twoPtr[i] = 1;
                Assert.IsTrue(one == two);
                Assert.IsFalse(one != two);

                onePtr[i] = 0;
                Assert.IsFalse(one == two);
                Assert.IsTrue(one != two);

                twoPtr[i] = 0;
            }
        }

        [Test]
        public unsafe void ManagedVsUnmanaged()
        {
            var rng = new Random();
            var input = new byte[100];
            rng.NextBytes(input);

            Md5Digest managed, unmanaged;

            UnsafeMd5.ComputeHash(input, out managed);

            fixed (byte* inputPtr = input)
                UnsafeMd5.ComputeHash(inputPtr, input.Length, &unmanaged);

            Assert.AreEqual(managed, unmanaged);
        }

        [Test]
        public unsafe void GetBytes()
        {
            var creationBytes = new byte[Md5Digest.SIZE];
            var rng = new Random();
            rng.NextBytes(creationBytes);

            fixed (byte* creationPtr = creationBytes)
            {
                var digest = (Md5Digest*)creationPtr;
                var output = digest->GetBytes();

                Assert.AreEqual(Md5Digest.SIZE, output.Length);

                for (var i = 0; i < output.Length; i++)
                {
                    Assert.AreEqual(creationBytes[i], output[i]);
                }
            }
        }

        [Test]
        public unsafe void RandomInputs()
        {
            var rng = new Random();
            var md5 = MD5.Create();
            
            var digest = default(Md5Digest);

            for (var byteLength = 0; byteLength < 10000; byteLength++)
            {
                for (var i = 0; i < 5; i++)
                {
                    var input = new byte[byteLength];
                    rng.NextBytes(input);

                    var expected = md5.ComputeHash(input);

                    fixed (byte* ptr = input)
                    {
                        UnsafeMd5.ComputeHash(ptr, input.Length, &digest);
                    }

                    AssertDigestsAreEqual(input, expected, ref digest);
                }
            }
        }

        static unsafe void AssertDigestsAreEqual(byte[] input, byte[] expected, ref Md5Digest actual)
        {
            Assert.AreEqual(Md5Digest.SIZE, expected.Length);

            fixed (byte* expPtr = expected)
            {
                var expectedDigestPtr = (Md5Digest*)expPtr;

                if (*expectedDigestPtr != actual)
                {
                    var msg = GetHashMismatchMessage(input, expected, actual.GetBytes());
                    Assert.Fail(msg);
                }
            }
        }

        static string GetHashMismatchMessage(byte[] input, byte[] expected, byte[] actual)
        {
            var sb = new StringBuilder();

            sb.AppendLine("MD5 Hash Mismatch");
            sb.AppendLine();

            sb.Append("Expected: ");
            WriteHexBytes(expected, sb);
            sb.AppendLine();

            sb.Append("Actual:   ");
            WriteHexBytes(actual, sb);
            sb.AppendLine();

            sb.Append("var input = ");
            WriteByteArray(input, sb);
            sb.AppendLine(";");
            sb.AppendLine();

            return sb.ToString();
        }

        static void WriteHexBytes(byte[] bytes, StringBuilder sb)
        {
            foreach (var b in bytes)
            {
                sb.Append(Convert.ToString(b, 16).PadLeft(2, '0'));
                sb.Append(' ');
            }
        }

        static void WriteByteArray(byte[] bytes, StringBuilder sb)
        {
            sb.Append('{');

            foreach (var b in bytes)
            {
                sb.Append(b);
                sb.Append(',');
            }

            sb.Append('}');
        }
    }
}