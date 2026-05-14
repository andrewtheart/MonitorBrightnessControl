namespace MonitorBrightness;

internal sealed class BrightnessUpdateQueue : IDisposable
{
    private readonly Dictionary<IntPtr, BrightnessWorker> _workers = new();
    private readonly Func<IntPtr, int, bool> _setBrightness;
    private readonly object _gate = new();

    public BrightnessUpdateQueue()
        : this(MonitorEnumerator.SetBrightness)
    {
    }

    internal BrightnessUpdateQueue(Func<IntPtr, int, bool> setBrightness)
    {
        _setBrightness = setBrightness;
    }

    public void SetLatest(IntPtr physicalMonitorHandle, string monitorName, int brightness)
    {
        if (physicalMonitorHandle == IntPtr.Zero)
        {
            AppLogger.Warn($"Brightness update skipped for {monitorName}: monitor handle is no longer valid");
            return;
        }

        BrightnessWorker worker;
        lock (_gate)
        {
            if (!_workers.TryGetValue(physicalMonitorHandle, out worker!))
            {
                worker = new BrightnessWorker(physicalMonitorHandle, monitorName, _setBrightness);
                _workers.Add(physicalMonitorHandle, worker);
            }
        }

        worker.SetLatest(brightness);
    }

    public void Dispose()
    {
        List<BrightnessWorker> workers;
        lock (_gate)
        {
            workers = _workers.Values.ToList();
            _workers.Clear();
        }

        foreach (var worker in workers)
        {
            worker.Dispose();
        }
    }

    private sealed class BrightnessWorker : IDisposable
    {
        private static readonly TimeSpan DisposeWaitTimeout = TimeSpan.FromSeconds(2);

        private readonly IntPtr _physicalMonitorHandle;
        private readonly string _monitorName;
        private readonly Func<IntPtr, int, bool> _setBrightness;
        private readonly object _gate = new();
        private int? _latestBrightness;
        private Task? _workerTask;
        private bool _disposed;

        public BrightnessWorker(IntPtr physicalMonitorHandle, string monitorName, Func<IntPtr, int, bool> setBrightness)
        {
            _physicalMonitorHandle = physicalMonitorHandle;
            _monitorName = monitorName;
            _setBrightness = setBrightness;
        }

        public void SetLatest(int brightness)
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _latestBrightness = brightness;
                _workerTask ??= Task.Run(ProcessUpdates);
            }
        }

        private void ProcessUpdates()
        {
            while (true)
            {
                int brightness;
                lock (_gate)
                {
                    if (_latestBrightness is not int latest)
                    {
                        _workerTask = null;
                        return;
                    }

                    brightness = latest;
                    _latestBrightness = null;
                }

                try
                {
                    if (!_setBrightness(_physicalMonitorHandle, brightness))
                        AppLogger.Warn($"Failed to set brightness for {_monitorName} to {brightness}%");
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"Brightness update failed for {_monitorName}", ex);
                }
            }
        }

        public void Dispose()
        {
            Task? workerTask;
            lock (_gate)
            {
                _disposed = true;
                _latestBrightness = null;
                workerTask = _workerTask;
            }

            if (workerTask is null)
                return;

            try
            {
                if (!workerTask.Wait(DisposeWaitTimeout))
                    AppLogger.Warn($"Timed out waiting for brightness update to finish for {_monitorName}");
            }
            catch (AggregateException ex)
            {
                AppLogger.Warn($"Brightness update failed for {_monitorName}", ex);
            }
        }
    }
}
