using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;

namespace CameraActivityMonitor;

public sealed class CameraActivityMonitor : IDisposable
{
    private readonly string _deviceId;
    private readonly SensorActivityCallback _callback;

    private IMFSensorActivityMonitor? _monitor;
    private bool _started;

    public bool IsInUse => _callback.IsInUse;

    public event Action<bool>? UsageChanged
    {
        add => _callback.UsageChanged += (inUse, pids) => value?.Invoke(inUse);
        remove => _callback.UsageChanged -= (inUse, pids) => value?.Invoke(inUse);
    }

    public CameraActivityMonitor(string deviceId)
    {
        _deviceId = deviceId;
        _callback = new SensorActivityCallback(_deviceId);
    }

    public void Start()
    {
        if (_started)
            return;

        PInvoke.MFCreateSensorActivityMonitor(_callback, out _monitor);

        _monitor.Start();
        _started = true;
    }

    public void Stop()
    {
        if (!_started)
            return;

        _monitor?.Stop();
        _monitor = null;
        _started = false;
    }

    public void Dispose()
    {
        Stop();
    }

    private class SensorActivityCallback : IMFSensorActivitiesReportCallback
    {
        private readonly string _deviceId;
        private readonly object _gate = new();

        public bool IsInUse { get; private set; }

        // bool: 是否占用, IReadOnlyCollection<uint>: 当前占用该设备的 PID 列表
        public event Action<bool, IReadOnlyCollection<uint>>? UsageChanged;

        private HashSet<uint> _activePids = [];

        public SensorActivityCallback(string deviceId)
        {
            _deviceId = deviceId;
        }

        public void OnActivitiesReport(IMFSensorActivitiesReport sensorActivitiesReport)
        {
            HashSet<uint> latestPids = [];
            try
            {
                sensorActivitiesReport.GetActivityReportByDeviceName(_deviceId, out var deviceActivityReport);
                deviceActivityReport.GetProcessCount(out uint count);

                for (uint i = 0; i < count; i++)
                {
                    deviceActivityReport.GetProcessActivity(i, out var processActivity);
                    processActivity.GetStreamingState(out BOOL streaming);

                    if (!streaming)
                    {
                        continue;
                    }

                    processActivity.GetProcessId(out uint processId);
                    latestPids.Add(processId);
                }
            }
            catch (COMException ex) when ((uint)ex.HResult == 0xC00D36D5)
            {
                // 设备当前没有活动
                latestPids.Clear();
            }

            bool inUse = latestPids.Count > 0;
            bool changed;
            uint[] snapshot;

            lock (_gate)
            {
                changed = (IsInUse != inUse) || !_activePids.SetEquals(latestPids);
                if (!changed)
                {
                    return;
                }

                IsInUse = inUse;
                _activePids = latestPids;
                snapshot = _activePids.ToArray();
            }

            UsageChanged?.Invoke(IsInUse, snapshot);
        }
    }
}