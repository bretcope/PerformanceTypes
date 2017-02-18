using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;

namespace PerformanceTypes.Tests
{
    [TestFixture]
    public class StopwatchTests
    {
        [Test]
        public void ResultMatchesStopwatch()
        {
            var sw = Stopwatch.StartNew();
            var ss = new StopwatchStruct();
            ss.Start();

            Thread.Sleep(500);

            sw.Stop();
            ss.Stop();

            var swMs = sw.Elapsed.TotalMilliseconds;
            var ssMs = ss.GetElapsedMilliseconds();

            var difference = Math.Abs(swMs - ssMs);

            Assert.IsTrue(difference < 1);
        }
    }
}