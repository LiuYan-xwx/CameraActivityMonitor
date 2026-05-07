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

    public event Action<bool, uint?>? UsageChanged
    {
        add => _callback.UsageChanged += value;
        remove => _callback.UsageChanged -= value;
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

        // bool: 是否占用, uint?: 当前占用该设备的 PID（无占用时为 null）
        public event Action<bool, uint?>? UsageChanged;

        private uint? _activePid;

        public SensorActivityCallback(string deviceId)
        {
            _deviceId = deviceId;
        }

        public void OnActivitiesReport(IMFSensorActivitiesReport sensorActivitiesReport)
        {
            uint? latestPid = null;

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
                    latestPid = processId;
                    break; // 同一时间只有一个 streaming=true
                }
            }
            catch (COMException ex) when ((uint)ex.HResult == 0xC00D36D5)
            {
                // 设备当前没有活动
                latestPid = null;
            }

            bool inUse = latestPid.HasValue;
            bool changed;

            lock (_gate)
            {
                changed = (IsInUse != inUse) || _activePid != latestPid;
                if (!changed)
                {
                    return;
                }

                IsInUse = inUse;
                _activePid = latestPid;
            }

            UsageChanged?.Invoke(IsInUse, _activePid);
        }
    }
}