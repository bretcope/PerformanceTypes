using System;
using System.Text;
using NUnit.Framework;

namespace PerformanceTypes.Tests
{
    [TestFixture]
    public class UnsafeTests
    {
        [Test]
        public unsafe void ToHexString()
        {
            var rng = new Random();
            for (var byteLength = 0; byteLength < 1500; byteLength++)
            {
                for (var i = 0; i < 5; i++)
                {
                    var bytes = new byte[byteLength];
                    rng.NextBytes(bytes);

                    fixed (byte* ptr = bytes)
                    {
                        var expected = ManagedToHexString(bytes);
                        var actual = Unsafe.ToHexString(ptr, bytes.Length);

                        Assert.AreEqual(expected, actual);
                    }
                }
            }
        }

        static string ManagedToHexString(byte[] bytes)
        {
            var sb = new StringBuilder();

            foreach (var b in bytes)
            {
                sb.Append(Convert.ToString(b, 16).PadLeft(2, '0'));
            }

            return sb.ToString();
        }
    }
}