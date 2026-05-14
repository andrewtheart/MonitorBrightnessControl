using System.Collections.Concurrent;
using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class BrightnessUpdateQueueTests
{
    [Fact]
    public void SetLatest_WithZeroHandle_DoesNotCallBrightnessSetter()
    {
        var calls = 0;
        using var queue = new BrightnessUpdateQueue((_, _) =>
        {
            calls++;
            return true;
        });

        queue.SetLatest(IntPtr.Zero, "Monitor", 50);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void SetLatest_SerializesWritesAndCoalescesToLatestPendingValue()
    {
        var handle = new IntPtr(123);
        var calls = new ConcurrentQueue<int>();
        var firstCallEntered = new ManualResetEventSlim();
        var releaseFirstCall = new ManualResetEventSlim();
        var secondCallEntered = new ManualResetEventSlim();
        var activeWriters = 0;
        var maxConcurrentWriters = 0;

        using var queue = new BrightnessUpdateQueue((_, brightness) =>
        {
            var active = Interlocked.Increment(ref activeWriters);
            maxConcurrentWriters = Math.Max(maxConcurrentWriters, active);
            calls.Enqueue(brightness);

            if (brightness == 10)
            {
                firstCallEntered.Set();
                Assert.True(releaseFirstCall.Wait(TimeSpan.FromSeconds(5)));
            }
            else
            {
                secondCallEntered.Set();
            }

            Interlocked.Decrement(ref activeWriters);
            return true;
        });

        queue.SetLatest(handle, "Monitor", 10);
        Assert.True(firstCallEntered.Wait(TimeSpan.FromSeconds(5)));

        queue.SetLatest(handle, "Monitor", 20);
        queue.SetLatest(handle, "Monitor", 30);

        releaseFirstCall.Set();
        Assert.True(secondCallEntered.Wait(TimeSpan.FromSeconds(5)));

        Assert.Equal(1, maxConcurrentWriters);
        Assert.Equal(new[] { 10, 30 }, calls.ToArray());
    }
}
