using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace PerformanceTypes.Tests
{
    public class UnsafeStringComparerTests
    {
        class Sample
        {
            /// <summary>
            /// A randomly generated string.
            /// </summary>
            public string StringA { get; }
            /// <summary>
            /// The char array version of StringA.
            /// </summary>
            public char[] CharsA { get; }
            /// <summary>
            /// A randomly generated string, guaranteed to be not equal to StringA.
            /// </summary>
            public string StringB { get; }
            /// <summary>
            /// The char array version of StringB.
            /// </summary>
            public char[] CharsB { get; }
            /// <summary>
            /// StringC is identical to StringA, except for the final character.
            /// </summary>
            public string StringC { get; }
            /// <summary>
            /// The char array version of StringC.
            /// </summary>
            public char[] CharsC { get; }

            public Sample(string a, string b)
            {
                StringA = a;
                CharsA = a.ToCharArray();
                StringB = b;
                CharsB = b.ToCharArray();

                CharsC = a.ToCharArray();
                CharsC[a.Length - 1] = (char)(CharsC[a.Length - 1] - 1);
                StringC = new string(CharsC);
            }
        }

        /// <summary>
        /// Each length will have three strings. The first two are completely random. The third is identical to the first except for the last character.
        /// </summary>
        static Dictionary<int, Sample> s_samplesByLength;

        [OneTimeSetUp]
        public void Setup()
        {
            var rng = new Random();
            s_samplesByLength = new Dictionary<int, Sample>();

            // add every length between 1 and 17
            for (var i = 1; i < 18; i++)
            {
                AddSampleStrings(i, rng);
            }

            // add values near powers of 2 up to about 10000
            for (var i = 32; i < 10000; i *= 2)
            {
                AddSampleStrings(i - 3, rng);
                AddSampleStrings(i - 2, rng);
                AddSampleStrings(i - 1, rng);
                AddSampleStrings(i - 0, rng);
                AddSampleStrings(i + 1, rng);
            }
        }

        static void AddSampleStrings(int len, Random rng)
        {
            var a = GenerateString(len, rng);

            string b;
            do { b = GenerateString(len, rng); }
            while (len > 0 && a == b); // make sure a and b are actually different.

            s_samplesByLength[len] = new Sample(a, b);
        }

        static string GenerateString(int len, Random rng)
        {
            var chars = new char[len];

            for (var i = 0; i < len; i++)
            {
                chars[i] = (char)rng.Next(40, char.MaxValue);
            }

            return new string(chars);
        }

        [Test]
        public void StringToCharArray()
        {
            // length checks
            Assert.True(UnsafeStringComparer.AreEqual("", new char[0]));
            Assert.False(UnsafeStringComparer.AreEqual("1", new char[0]));
            Assert.False(UnsafeStringComparer.AreEqual("", new char[1]));
            Assert.False(UnsafeStringComparer.AreEqual("1", new char[2]));
            Assert.False(UnsafeStringComparer.AreEqual("11", new char[1]));

            // invalid length for char buffer (outside bounds of buffer)
            Assert.False(UnsafeStringComparer.AreEqual("123", "123".ToCharArray(), 1, 3));

            // starting at an offset greater than zero
            Assert.True(UnsafeStringComparer.AreEqual("123", "0123".ToCharArray(), 1, 3));
            
            foreach (var sample in s_samplesByLength.Values)
            {
                AssertEqual(sample.StringA, sample.CharsA);
                AssertEqual(sample.StringB, sample.CharsB);
                AssertEqual(sample.StringC, sample.CharsC);

                AssertNotEqual(sample.StringA, sample.CharsB);
                AssertNotEqual(sample.StringA, sample.CharsC);
                AssertNotEqual(sample.StringB, sample.CharsA);
                AssertNotEqual(sample.StringB, sample.CharsC);
                AssertNotEqual(sample.StringC, sample.CharsA);
                AssertNotEqual(sample.StringC, sample.CharsB);
            }
        }

        static void AssertEqual(string s, char[] chars)
        {
            Assert.True(UnsafeStringComparer.AreEqual(s, chars), $"Didn't match. String: \"{s}\", Chars: \"{new string(chars)}\"");
        }

        static void AssertNotEqual(string s, char[] chars)
        {
            Assert.False(UnsafeStringComparer.AreEqual(s, chars), $"Incorrect match. String: \"{s}\", Chars: \"{new string(chars)}\"");
        }

        [Test]
        public void StringToCharPointer()
        {
            foreach (var sample in s_samplesByLength.Values)
            {
                AssertEqualUnsafe(sample.StringA, sample.CharsA);
                AssertEqualUnsafe(sample.StringB, sample.CharsB);
                AssertEqualUnsafe(sample.StringC, sample.CharsC);

                AssertNotEqualUnsafe(sample.StringA, sample.CharsB);
                AssertNotEqualUnsafe(sample.StringA, sample.CharsC);
                AssertNotEqualUnsafe(sample.StringB, sample.CharsA);
                AssertNotEqualUnsafe(sample.StringB, sample.CharsC);
                AssertNotEqualUnsafe(sample.StringC, sample.CharsA);
                AssertNotEqualUnsafe(sample.StringC, sample.CharsB);
            }
        }

        static unsafe void AssertEqualUnsafe(string s, char[] chars)
        {
            fixed (char* ptr = chars)
            {
                Assert.True(UnsafeStringComparer.AreEqual(s, ptr), $"Didn't match. String: \"{s}\", Chars: \"{new string(chars)}\"");
            }
        }

        static unsafe void AssertNotEqualUnsafe(string s, char[] chars)
        {
            fixed (char* ptr = chars)
            {
                Assert.False(UnsafeStringComparer.AreEqual(s, ptr), $"Incorrect match. String: \"{s}\", Chars: \"{new string(chars)}\"");
            }
        }

        [Test]
        public void CharPointerToCharPointer()
        {
            foreach (var sample in s_samplesByLength.Values)
            {
                AssertEqualUnsafe(sample.CharsA, sample.CharsA);
                AssertEqualUnsafe(sample.CharsB, sample.CharsB);
                AssertEqualUnsafe(sample.CharsC, sample.CharsC);

                AssertNotEqualUnsafe(sample.CharsA, sample.CharsB);
                AssertNotEqualUnsafe(sample.CharsA, sample.CharsC);
                AssertNotEqualUnsafe(sample.CharsB, sample.CharsA);
                AssertNotEqualUnsafe(sample.CharsB, sample.CharsC);
                AssertNotEqualUnsafe(sample.CharsC, sample.CharsA);
                AssertNotEqualUnsafe(sample.CharsC, sample.CharsB);
            }
        }

        static unsafe void AssertEqualUnsafe(char[] a, char[] b)
        {
            fixed (char* aPtr = a)
            fixed (char* bPtr = b)
            {
                Assert.True(UnsafeStringComparer.AreEqual(aPtr, bPtr, a.Length), $"Didn't match. String: \"{new string(a)}\", Chars: \"{new string(b)}\"");
            }
        }

        static unsafe void AssertNotEqualUnsafe(char[] a, char[] b)
        {
            fixed (char* aPtr = a)
            fixed (char* bPtr = b)
            {
                Assert.False(UnsafeStringComparer.AreEqual(aPtr, bPtr, a.Length), $"Incorrect match. String: \"{new string(a)}\", Chars: \"{new string(b)}\"");
            }
        }
    }
}