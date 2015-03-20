# Performance Types

Currently, the only type in this library is [StopwatchStruct](https://github.com/bretcope/PerformanceTypes/blob/master/PerformanceTypes/StopwatchStruct.cs). It is a partial re-implementation of the [Stopwatch](https://msdn.microsoft.com/en-us/library/system.diagnostics.stopwatch) class with methods for `Start()`, `Stop()`, and `GetElapsedMilliseconds()` plus an `ElapsedTicks` property. The struct itself is 17 bytes.
