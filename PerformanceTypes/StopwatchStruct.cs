using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PerformanceTypes
{
    /// <summary>
    /// A value-type implementation of Stopwatch. It allows for high precision benchmarking without needing to instantiate reference types on the managed heap.
    /// Because of native calls to QueryPerformanceCounter, this struct will only work on Windows.
    /// 
    /// Note: because this is a struct, instances will be passed by value. This means every time you assign an instance to another variable, or pass it as a
    /// parameter, a copy is made. Calling methods like Stop() on one copy, will not have any effect on other copies. Unless you are comfortable with the
    /// implications of a pass-by-value mutable struct, it is highly recommended that you only assign each instance to one variable, and do not pass it to
    /// other methods.
    /// </summary>
    public struct StopwatchStruct
    {
        long _startTimestamp;
        long _elapsedCounts;

        /// <summary>
        /// True if the stopwatch is running. In other words, Start() has been called, but Stop() has not.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// The number of "counts" that the stopwatch has run. This property is accurate, even while the stopwatch is running.
        /// </summary>
        public long ElapsedCounts
        {
            get
            {
                if (IsRunning)
                {
                    long timestamp;
                    QueryPerformanceCounter(out timestamp);
                    return _elapsedCounts + (timestamp - _startTimestamp);
                }

                return _elapsedCounts;
            }
        }

        /// <summary>
        /// Gets the total elapsed time that the stopwatch has run. This property is accurate, even while the stopwatch is running.
        /// </summary>
        public TimeSpan Elapsed => TimeSpan.FromMilliseconds(GetElapsedMilliseconds());

        /// <summary>
        /// Starts the stopwatch by marking the start time.
        /// </summary>
        public void Start()
        {
            if (!CanUse)
                throw new Exception("Unable to use QueryPerformanceCounter. The StopwatchStruct only supports high-resolution timings currently.");

            if (!IsRunning)
            {
                IsRunning = true;
                QueryPerformanceCounter(out _startTimestamp);
            }
        }

        /// <summary>
        /// Stops the stopwatch, if it was running. Calculates the elapsed ticks since it was last started, and adds them to <see cref="ElapsedCounts"/>.
        /// </summary>
        public void Stop()
        {
            if (IsRunning)
            {
                long timestamp;
                QueryPerformanceCounter(out timestamp);
                IsRunning = false;
                _elapsedCounts += timestamp - _startTimestamp;
            }
        }

        /// <summary>
        /// Returns the total number of milliseconds that the stopwatch has run for. This property is accurate, even while the stopwatch is running.
        /// </summary>
        public double GetElapsedMilliseconds()
        {
            return ElapsedCounts / CountsPerMillisecond;
        }

        /****************************************************************************************
        *
        * Static Members
        *
        ****************************************************************************************/

        /// <summary>
        /// Returns true if the Stopwatch struct can be used on the current system. Only Windows is supported.
        /// </summary>
        public static bool CanUse { get; }
        /// <summary>
        /// True if high-resolution timing is available. Currently this is always true if <see cref="CanUse"/> is true.
        /// </summary>
        public static bool IsHighResolution => CanUse;
        /// <summary>
        /// The resolution of QueryPerformanceCounter (in "counts" per second).
        /// </summary>
        public static long CountsPerSecond { get; }
        /// <summary>
        /// The resolution of QueryPerformanceCounter (in "counts" per second).
        /// </summary>
        public static double CountsPerMillisecond { get; }
        /// <summary>
        /// If an exception occurred during initialization, it will be available here.
        /// </summary>
        public static Exception InitializationException { get; }

        static StopwatchStruct()
        {
            try
            {
                long countsPerSecond;
                var succeeded = QueryPerformanceFrequency(out countsPerSecond);

                if (succeeded)
                {
                    CanUse = true;
                    CountsPerSecond = countsPerSecond;
                    CountsPerMillisecond = countsPerSecond / 1000.0;
                }
            }
            catch (Exception ex)
            {
                InitializationException = ex;
            }
        }

        /// <summary>
        /// Calls the WinAPI QueryPerformanceCounter method (the Windows high-resolution time).
        /// See: https://msdn.microsoft.com/en-us/library/windows/desktop/ms644904(v=vs.85).aspx
        /// </summary>
        /// <param name="value">The current high-resolution time value in ticks.</param>
        /// <returns>True if the call succeeded.</returns>
        [DllImport("kernel32.dll")]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool QueryPerformanceCounter(out long value);

        /// <summary>
        /// Calls the WinAPI QueryPerformanceFrequency method. This method allows you to determine the resolution of QueryPerformanceCounter.
        /// See: https://msdn.microsoft.com/en-us/library/windows/desktop/ms644905(v=vs.85).aspx
        /// </summary>
        /// <param name="value">The number of ticks </param>
        /// <returns>True if the call succeeded.</returns>
        [DllImport("kernel32.dll")]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool QueryPerformanceFrequency(out long value);
    }
}