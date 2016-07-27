using System;

namespace PerformanceTypes
{
    public static class UnsafeStringComparer
    {
        /// <summary>
        /// Compares a string to the characters in a buffer.
        /// </summary>
        /// <param name="str">The string to compare against the character buffer.</param>
        /// <param name="buffer">The buffer to compare against the string.</param>
        /// <returns>True if the characters in the string and buffer were equal.</returns>
        public static bool AreEqual(string str, char[] buffer)
        {
            return AreEqual(str, buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Compares a string to the characters in a buffer.
        /// </summary>
        /// <param name="str">The string to compare against the character buffer.</param>
        /// <param name="buffer">The buffer to compare against the string.</param>
        /// <param name="start">The offset into the buffer where comparison with the string should start.</param>
        /// <param name="count">
        /// The length of characters in the buffer to compare against the string.
        /// If length and str.Length don't match, this method will always return false
        /// </param>
        /// <returns>True if the characters in the string and buffer were equal.</returns>
        public static unsafe bool AreEqual(string str, char[] buffer, int start, int count)
        {
            if (str.Length != count)
                return false;

            if (start + count > buffer.Length)
                return false;

            switch (count)
            {
                case 0:
                    return true;
                case 1:
                    return str[0] == buffer[start + 0];
                case 2:
                    return str[0] == buffer[start + 0]
                        && str[1] == buffer[start + 1];
                case 3:
                    return str[0] == buffer[start + 0]
                        && str[1] == buffer[start + 1]
                        && str[2] == buffer[start + 2];
                case 4:
                    return str[0] == buffer[start + 0]
                        && str[1] == buffer[start + 1]
                        && str[2] == buffer[start + 2]
                        && str[3] == buffer[start + 3];
                case 5:
                    return str[0] == buffer[start + 0]
                        && str[1] == buffer[start + 1]
                        && str[2] == buffer[start + 2]
                        && str[3] == buffer[start + 3]
                        && str[4] == buffer[start + 4];
                case 6:
                    return str[0] == buffer[start + 0]
                        && str[1] == buffer[start + 1]
                        && str[2] == buffer[start + 2]
                        && str[3] == buffer[start + 3]
                        && str[4] == buffer[start + 4]
                        && str[5] == buffer[start + 5];
                case 7:
                    return str[0] == buffer[start + 0]
                        && str[1] == buffer[start + 1]
                        && str[2] == buffer[start + 2]
                        && str[3] == buffer[start + 3]
                        && str[4] == buffer[start + 4]
                        && str[5] == buffer[start + 5]
                        && str[6] == buffer[start + 6];
            }

            // eight characters or more makes it worth it to switch into unsafe more and read by int64 instead of by char.
            fixed (char* sPtr = str)
            fixed (char* bPtr = buffer)
            {
                return CompareByLong(sPtr, bPtr + start, count);
            }
        }

        /// <summary>
        /// Compares a string to the characters in a buffer.
        /// </summary>
        /// <param name="str">The string to compare against the character buffer.</param>
        /// <param name="buffer">
        /// A pointer to the first character to compare in a buffer.
        /// The buffer must have least length chars remaining to prevent reading out of bounds.
        /// </param>
        /// <param name="count">The number of characters in the buffer to compare. If length does not equal str.Length, this method will always return false.</param>
        /// <returns>True if the characters in the string and buffer were equal.</returns>
        public static unsafe bool AreEqual(string str, char* buffer, int count)
        {
            if (str.Length != count)
                return false;

            switch (count)
            {
                case 0:
                    return true;
                case 1:
                    return str[0] == buffer[0];
                case 2:
                    return str[0] == buffer[0]
                        && str[1] == buffer[1];
                case 3:
                    return str[0] == buffer[0]
                        && str[1] == buffer[1]
                        && str[2] == buffer[2];
                case 4:
                    return str[0] == buffer[0]
                        && str[1] == buffer[1]
                        && str[2] == buffer[2]
                        && str[3] == buffer[3];
                case 5:
                    return str[0] == buffer[0]
                        && str[1] == buffer[1]
                        && str[2] == buffer[2]
                        && str[3] == buffer[3]
                        && str[4] == buffer[4];
                case 6:
                    return str[0] == buffer[0]
                        && str[1] == buffer[1]
                        && str[2] == buffer[2]
                        && str[3] == buffer[3]
                        && str[4] == buffer[4]
                        && str[5] == buffer[5];
                case 7:
                    return str[0] == buffer[0]
                        && str[1] == buffer[1]
                        && str[2] == buffer[2]
                        && str[3] == buffer[3]
                        && str[4] == buffer[4]
                        && str[5] == buffer[5]
                        && str[6] == buffer[6];
                default:
                    fixed (char* sPtr = str)
                    {
                        return CompareByLong(sPtr, buffer, count);
                    }
            }
        }

        /// <summary>
        /// Compares two character buffers against each other.
        /// </summary>
        /// <param name="aPtr">A pointer to the first character to compare in the first buffer.</param>
        /// <param name="bPtr">A pointer to the first character to compare in the second buffer.</param>
        /// <param name="count">The number of characters to compare in each buffer.</param>
        /// <returns>True if the characters in the two buffers were equal.</returns>
        public static unsafe bool AreEqual(char* aPtr, char* bPtr, int count)
        {
            if (aPtr == bPtr) // they point to the same memory, so they're definitely equal
                return true;

            switch (count)
            {
                case 0:
                    return true;
                case 1:
                    return aPtr[0] == bPtr[0];
                case 2:
                    return aPtr[0] == bPtr[0]
                        && aPtr[1] == bPtr[1];
                case 3:
                    return aPtr[0] == bPtr[0]
                        && aPtr[1] == bPtr[1]
                        && aPtr[2] == bPtr[2];
                case 4:
                    return aPtr[0] == bPtr[0]
                        && aPtr[1] == bPtr[1]
                        && aPtr[2] == bPtr[2]
                        && aPtr[3] == bPtr[3];
                case 5:
                    return aPtr[0] == bPtr[0]
                        && aPtr[1] == bPtr[1]
                        && aPtr[2] == bPtr[2]
                        && aPtr[3] == bPtr[3]
                        && aPtr[4] == bPtr[4];
                case 6:
                    return aPtr[0] == bPtr[0]
                        && aPtr[1] == bPtr[1]
                        && aPtr[2] == bPtr[2]
                        && aPtr[3] == bPtr[3]
                        && aPtr[4] == bPtr[4]
                        && aPtr[5] == bPtr[5];
                case 7:
                    return aPtr[0] == bPtr[0]
                        && aPtr[1] == bPtr[1]
                        && aPtr[2] == bPtr[2]
                        && aPtr[3] == bPtr[3]
                        && aPtr[4] == bPtr[4]
                        && aPtr[5] == bPtr[5]
                        && aPtr[6] == bPtr[6];
                default:
                    return CompareByLong(aPtr, bPtr, count);
            }
        }


        static unsafe bool CompareByLong(char* aPtr, char* bPtr, int count)
        {
            const int divisor = sizeof(long) / sizeof(char);
            var longs = count / divisor;
            var remainder = count % divisor;

            var aLong = (long*)aPtr;
            var bLong = (long*)bPtr;

            var longsEnd = aLong + longs;
            while (aLong < longsEnd)
            {
                if (*aLong != *bLong)
                    return false;

                aLong++;
                bLong++;
            }

            var aChar = (char*)aLong;
            var bChar = (char*)bLong;

            switch (remainder)
            {
                case 1:
                    return aChar[0] == bChar[0];
                case 2:
                    return aChar[0] == bChar[0]
                        && aChar[1] == bChar[1];
                case 3:
                    return aChar[0] == bChar[0]
                        && aChar[1] == bChar[1]
                        && aChar[2] == bChar[2];
                default:
                    return true;
            }
        }
    }
}