using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PerformanceTypes
{
    public struct StopwatchStruct
    {
        long _startTimestamp;
        long _elapsed;

        public bool IsRunning { get; private set; }

        public long ElapsedTicks
        {
            get
            {
                if (IsRunning)
                {
                    long timestamp;
                    QueryPerformanceCounter(out timestamp);
                    return _elapsed + (timestamp - _startTimestamp);
                }

                return _elapsed;
            }
        }

        public void Start()
        {
            if (!IsRunning)
            {
                IsRunning = true;
                QueryPerformanceCounter(out _startTimestamp);
            }
        }

        public void Stop()
        {
            if (IsRunning)
            {
                long timestamp;
                QueryPerformanceCounter(out timestamp);
                IsRunning = false;
                _elapsed += timestamp - _startTimestamp;
            }
        }

        public double GetElapsedMilliseconds()
        {
            return (double)ElapsedTicks / TICKS_PER_MILLISECOND;
        }

        /****************************************************************************************
        *
        * Static Members
        *
        ****************************************************************************************/

        const long TICKS_PER_MILLISECOND = 10000;
        const long TICKS_PER_SECOND = TICKS_PER_MILLISECOND * 1000;

        public static readonly bool IsHighResolution;
        public static readonly long Frequency;
        public static readonly double TickFrequency;

        static StopwatchStruct()
        {
            var succeeded = QueryPerformanceFrequency(out Frequency);
            if (!succeeded)
                throw new Exception("Unable to use QueryPerformanceCounter. The StopwatchStruct only supports high-resolution timings currently.");

            IsHighResolution = true;
            TickFrequency = (double)TICKS_PER_SECOND / Frequency;
        }

        [DllImport("kernel32.dll")]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool QueryPerformanceCounter(out long value);

        [DllImport("kernel32.dll")]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool QueryPerformanceFrequency(out long value);
    }
}