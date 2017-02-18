namespace PerformanceTypes
{
    /// <summary>
    /// Unsafe memory utilities.
    /// </summary>
    public static class Unsafe
    {
        /// <summary>
        /// This method is not a general-purpose replacement for memcpy or Marshal.Copy, however, it will generally out-perform those methods for byte lengths
        /// of around 400 or less.
        /// </summary>
        /// <param name="src">Buffer to copy from.</param>
        /// <param name="dest">Buffer to copy to.</param>
        /// <param name="count">Number of bytes to copy.</param>
        public static unsafe void MemoryCopy(byte* src, byte* dest, int count)
        {
            var remainder = count;

            if (count > 8)
            {
                var longs = count / 8;
                remainder = count % 8;

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