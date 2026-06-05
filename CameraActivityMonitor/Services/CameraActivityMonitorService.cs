using Avalonia.Threading;
using CameraActivityMonitor.Helpers;
using CameraActivityMonitor.Models;
using ClassIsland.Core.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;

namespace CameraActivityMonitor.Services
{
    public partial class CameraActivityMonitorService : ObservableObject, ICameraActivityMonitorService
    {
        public bool IsCameraInUse { get; private set; }
        public bool IsMonitoring => _monitor is not null;
        public string? CurrentCameraId { get; private set; }

        [ObservableProperty]
        private string? _processName;

        public event Action<bool>? UsageChanged;

        private CameraActivityMonitor? _monitor;
        private readonly IRulesetService _rulesetService;

        public CameraActivityMonitorService(IRulesetService rulesetService)
        {
            _rulesetService = rulesetService;
            _rulesetService.RegisterRuleHandler("cameraactivitymonitor.iscamerainuse", CameraRuleHandler);
        }

        private bool CameraRuleHandler(object? o)
        {
            if (o is CameraRuleSettings settings)
            {
                return IsCameraInUse && (!settings.MatchProcessName || string.Equals(ProcessName, settings.ProcessName, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }

        public async Task<IReadOnlyList<CameraDeviceInfo>> GetAllCamerasAsync()
        {
            var devices = await CameraDeviceHelper.GetAllCamerasAsync();
            return devices
                .Select(d => new CameraDeviceInfo
                {
                    Id = d.Id,
                    Name = d.Name
                })
                .ToList();
        }

        public void StartMonitoring(string cameraId)
        {
            if (CurrentCameraId == cameraId && _monitor is not null)
                return;

            StopMonitoring();

            var monitor = new CameraActivityMonitor(cameraId);
            monitor.UsageChanged += OnUsageChanged;
            monitor.Start();

            _monitor = monitor;
            CurrentCameraId = cameraId;
            IsCameraInUse = monitor.IsInUse;
        }
        public void StopMonitoring()
        {
            if (_monitor is not null)
            {
                IsCameraInUse = false;
                _monitor.UsageChanged -= OnUsageChanged;
                _monitor.Dispose();
                _monitor = null;
            }

            CurrentCameraId = null;
            IsCameraInUse = false;
            ProcessName = null;
        }

        private void OnUsageChanged(bool inUse, uint? pid)
        {
            IsCameraInUse = inUse;
            try
            {
                if (pid.HasValue)
                {
                    using var process = Process.GetProcessById((int)pid);
                    ProcessName = process.ProcessName;
                }
                else
                {
                    ProcessName = null;
                }
            }
            catch (ArgumentException)
            {
                ProcessName = null;
            }
            catch (InvalidOperationException)
            {
                ProcessName = null;
            }
            UsageChanged?.Invoke(inUse);
            Dispatcher.UIThread.Post(() => _rulesetService.NotifyStatusChanged());
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}