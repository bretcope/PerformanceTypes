using NUnit.Framework;

namespace PerformanceTypes.Tests
{
    [TestFixture]
    public class StringSetTests
    {
        [Test]
        public void StringEquals()
        {
            const string original = "thisthatthen";
            var arr = original.ToCharArray();

            var set = new StringSet(10);

            string str;
            set.Add(arr, 0, 4, out str);
            Assert.AreEqual("this", str);

            set.Add(arr, 4, 4, out str);
            Assert.AreEqual("that", str);

            set.Add(arr, 8, 4, out str);
            Assert.AreEqual("then", str);
        }

        [Test]
        public void ReferenceEquals()
        {
            const string original = "thisthatthen";
            var arr = original.ToCharArray();

            var set = new StringSet(10);

            string this1, that1, then1;
            set.Add(arr, 0, 4, out this1);
            set.Add(arr, 4, 4, out that1);
            set.Add(arr, 8, 4, out then1);

            string this2, that2, then2;
            set.Add(arr, 0, 4, out this2);
            set.Add(arr, 4, 4, out that2);
            set.Add(arr, 8, 4, out then2);

            Assert.AreEqual(this1, this2);
            Assert.AreEqual(that1, that2);
            Assert.AreEqual(then1, then2);

            Assert.AreSame(this1, this2);
            Assert.AreSame(that1, that2);
            Assert.AreSame(then1, then2);
        }

        [Test]
        public void Grow()
        {
            //                       0  3  6    11  15  19 22   27   32  36
            const string original = "onetwothreefourfivesixseveneightnineten";
            var arr = original.ToCharArray();

            var set = new StringSet(4);

            string str;
            set.Add(arr, 0, 3, out str); // one
            set.Add(arr, 3, 3, out str); // two
            set.Add(arr, 6, 5, out str); // three
            set.Add(arr, 11, 4, out str); // four
            Assert.AreEqual(4, set.MaxSize);

            // make sure we can find something
            Assert.AreEqual("two", set.GetExistingString(arr, 3, 3));

            set.Add(arr, 15, 4, out str); // five
            Assert.Greater(set.MaxSize, 4, "The set should have expanded to greater than 4 maximum size.");

            set.Add(arr, 19, 3, out str); // six
            set.Add(arr, 22, 5, out str); // seven
            set.Add(arr, 27, 5, out str); // eight
            set.Add(arr, 32, 4, out str); // nine
            set.Add(arr, 36, 3, out str); // ten

            Assert.AreEqual(10, set.Count);
            Assert.GreaterOrEqual(set.MaxSize, set.Count);
            
            Assert.AreEqual("two", set.GetExistingString(arr, 3, 3));
            Assert.AreEqual("seven", set.GetExistingString(arr, 22, 5));
        }

        [Test]
        public void GetExisting()
        {
            const string original = "thisthatthen";
            var arr = original.ToCharArray();

            var set = new StringSet(10);

            Assert.IsNull(set.GetExistingString(arr, 0, 4));

            string str;
            Assert.True(set.Add(arr, 0, 4, out str));
            Assert.False(set.Add(arr, 0, 4, out str));
            Assert.AreEqual(str, set.GetExistingString(arr, 0, 4));

            Assert.IsNull(set.GetExistingString(arr, 4, 4));
        }

        [Test]
        public void SearchCursor()
        {
            var one = "one";
            var two = "two";
            var three = "three";

            var set = new StringSet(4);

            // pretend that we have some hash collisions
            var hash = StringHash.GetHash(one);

            set.Add(one, hash);
            set.Add(two, hash);
            set.Add(three, hash);

            var cursor = set.GetSearchCursor(hash);

            // add one more with the same hash to make sure the cursor doesn't change
            set.Add("four", hash);

            Assert.AreEqual(4, set.Count);
            Assert.AreEqual(4, set.MaxSize); // hash collisions shouldn't cause the set to grow

            Assert.True(cursor.HasMore);
            Assert.AreSame(three, cursor.NextString());
            Assert.True(cursor.HasMore);
            Assert.AreSame(two, cursor.NextString());
            Assert.True(cursor.HasMore);
            Assert.AreSame(one, cursor.NextString());
            Assert.False(cursor.HasMore);
        }
    }
}