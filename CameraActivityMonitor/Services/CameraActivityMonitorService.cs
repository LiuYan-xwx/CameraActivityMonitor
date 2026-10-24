using Avalonia.Threading;
using CameraActivityMonitor.Helpers;
using CameraActivityMonitor.Models;
using ClassIsland.Core.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;

namespace CameraActivityMonitor.Services
{
    public partial class CameraActivityMonitorService : ObservableObject, ICameraActivityMonitorService
    {
        public bool IsCameraInUse { get; private set; }
        public bool IsMonitoring => _nativeMonitor is not null || _registryMonitor is not null;
        public string? CurrentCameraId { get; private set; }

        [ObservableProperty]
        private string? _processName;

        [ObservableProperty]
        private string? _registryStatus;

        [ObservableProperty]
        private string? _registryStatusDetails;

        [ObservableProperty]
        private bool _isMediaFoundationMonitoring;

        [ObservableProperty]
        private bool _isRegistryMonitoring;

        public event Action<bool>? UsageChanged;
        public event Action? MonitoringStateChanged;

        private NativeCameraActivityMonitor? _nativeMonitor;
        private Action<bool, uint?>? _nativeUsageHandler;
        private string? _nativeProcessName;
        private bool _nativeInUse;

        private RegistryCameraActivityMonitor? _registryMonitor;
        private Action<RegistryMonitorSnapshot>? _registryStateHandler;
        private RegistryMonitorSnapshot? _registrySnapshot;

        private readonly IRulesetService _rulesetService;
        private readonly ILogger<CameraActivityMonitorService> _logger;
        private string[] _activeProcessNames = [];
        private bool _monitoringRequested;
        private bool _disposed;
        private bool _lastIsMonitoring;

        public CameraActivityMonitorService(
            IRulesetService rulesetService,
            ILogger<CameraActivityMonitorService> logger)
        {
            _rulesetService = rulesetService;
            _logger = logger;
            _rulesetService.RegisterRuleHandler("cameraactivitymonitor.iscamerainuse", CameraRuleHandler);
            Plugin.Settings.PropertyChanged += OnSettingsPropertyChanged;
            UpdateRegistryIdleStatus();
            UpdateMonitoringState();
        }

        private bool CameraRuleHandler(object? o)
        {
            if (o is not CameraRuleSettings settings || !IsCameraInUse)
            {
                return false;
            }

            if (!settings.MatchProcessName)
            {
                return true;
            }

            var expectedProcessName = NormalizeProcessName(settings.ProcessName);
            if (string.IsNullOrWhiteSpace(expectedProcessName))
            {
                return true;
            }

            var activeProcessNames = _activeProcessNames;
            foreach (var processName in activeProcessNames)
            {
                if (string.Equals(processName, expectedProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? NormalizeProcessName(string? processName)
        {
            var normalized = processName?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (!normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            normalized = normalized[..^4].Trim();
            var lastSeparator = normalized.LastIndexOfAny(new[] { '\\', '/' });
            if (lastSeparator >= 0)
            {
                normalized = normalized[(lastSeparator + 1)..].Trim();
            }

            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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

        public void StartMonitoring(string? cameraId)
        {
            if (_disposed)
            {
                return;
            }

            _monitoringRequested = true;
            var targetCameraId = string.IsNullOrWhiteSpace(cameraId) ? null : cameraId.Trim();

            if (Plugin.Settings.EnableMediaFoundationMonitoring && targetCameraId is not null)
            {
                if (_nativeMonitor is null ||
                    !string.Equals(CurrentCameraId, targetCameraId, StringComparison.Ordinal))
                {
                    StopNativeMonitoringCore();
                    CurrentCameraId = targetCameraId;
                    StartNativeMonitoringCore(targetCameraId);
                }
                else
                {
                    CurrentCameraId = targetCameraId;
                }
            }
            else
            {
                StopNativeMonitoringCore();
                CurrentCameraId = targetCameraId;
            }

            if (Plugin.Settings.EnableRegistryMonitoring)
            {
                if (_registryMonitor is null)
                {
                    StartRegistryMonitoringCore();
                }
            }
            else
            {
                StopRegistryMonitoringCore();
                UpdateRegistryIdleStatus();
            }

            UpdateMonitoringState();
        }

        public void StopMonitoring()
        {
            _monitoringRequested = false;
            StopNativeMonitoringCore();
            CurrentCameraId = null;
            StopRegistryMonitoringCore();
            UpdateRegistryIdleStatus();
            UpdateMonitoringState();
        }

        public void StopMediaFoundationMonitoring()
        {
            StopNativeMonitoringCore();
            CurrentCameraId = null;
            UpdateMonitoringState();
        }

        private void StartNativeMonitoringCore(string cameraId)
        {
            var monitor = new NativeCameraActivityMonitor(cameraId);
            Action<bool, uint?> handler = (inUse, pid) => OnNativeUsageChanged(monitor, inUse, pid);

            _nativeMonitor = monitor;
            _nativeUsageHandler = handler;
            _nativeInUse = monitor.IsInUse;
            _nativeProcessName = null;
            monitor.UsageChanged += handler;

            try
            {
                monitor.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动 Media Foundation 摄像头监控失败。");
                monitor.UsageChanged -= handler;
                try
                {
                    monitor.Dispose();
                }
                catch (Exception disposeException)
                {
                    _logger.LogWarning(disposeException, "清理 Media Foundation 摄像头监控失败。");
                }

                _nativeMonitor = null;
                _nativeUsageHandler = null;
                _nativeInUse = false;
                _nativeProcessName = null;
            }

            RecomputeUsage();
        }

        private void StopNativeMonitoringCore()
        {
            var monitor = _nativeMonitor;
            var handler = _nativeUsageHandler;
            _nativeMonitor = null;
            _nativeUsageHandler = null;
            _nativeInUse = false;
            _nativeProcessName = null;

            if (monitor is not null)
            {
                if (handler is not null)
                {
                    monitor.UsageChanged -= handler;
                }

                try
                {
                    monitor.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "停止 Media Foundation 摄像头监控失败。");
                }
            }

            RecomputeUsage();
        }

        private void OnNativeUsageChanged(NativeCameraActivityMonitor monitor, bool inUse, uint? pid)
        {
            var processName = ResolveProcessName(pid);
            PostToUiThread(() =>
            {
                if (!ReferenceEquals(_nativeMonitor, monitor))
                {
                    return;
                }

                _nativeInUse = inUse;
                _nativeProcessName = processName;
                RecomputeUsage();
            });
        }

        private static string? ResolveProcessName(uint? pid)
        {
            if (pid is null)
            {
                return null;
            }

            try
            {
                using var process = Process.GetProcessById((int)pid.Value);
                return process.ProcessName;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return null;
            }
        }

        private void StartRegistryMonitoringCore()
        {
            var paths = ParseRegistryKeyPaths();
            if (paths.Count == 0)
            {
                StopRegistryMonitoringCore();
                RegistryStatus = "未配置注册表项路径";
                RegistryStatusDetails = "";
                UpdateMonitoringState();
                return;
            }

            var monitor = new RegistryCameraActivityMonitor(
                paths,
                Plugin.Settings.RegistryPollingIntervalSeconds,
                _logger);
            var initialSnapshot = monitor.InitialSnapshot;

            if (!monitor.HasValidTargets)
            {
                monitor.Dispose();
                StopRegistryMonitoringCore();
                RegistryStatus = "没有可用的注册表路径";
                RegistryStatusDetails = BuildRegistryDetails(initialSnapshot);
                UpdateMonitoringState();
                return;
            }

            _registryMonitor = monitor;
            _registrySnapshot = initialSnapshot;
            RegistryStatus = "正在读取…";
            RegistryStatusDetails = BuildRegistryDetails(initialSnapshot);

            Action<RegistryMonitorSnapshot> handler = snapshot => OnRegistrySnapshot(monitor, snapshot);
            _registryStateHandler = handler;
            monitor.StateChanged += handler;

            try
            {
                monitor.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动注册表摄像头监控失败。");
                monitor.StateChanged -= handler;
                try
                {
                    monitor.Dispose();
                }
                catch (Exception disposeException)
                {
                    _logger.LogWarning(disposeException, "清理注册表摄像头监控失败。");
                }

                _registryMonitor = null;
                _registryStateHandler = null;
                _registrySnapshot = null;
                RegistryStatus = "启动注册表监控失败";
                RegistryStatusDetails = BuildRegistryDetails(initialSnapshot);
            }

            RecomputeUsage();
            UpdateMonitoringState();
        }

        private void StopRegistryMonitoringCore()
        {
            var monitor = _registryMonitor;
            var handler = _registryStateHandler;
            _registryMonitor = null;
            _registryStateHandler = null;
            _registrySnapshot = null;

            if (monitor is not null)
            {
                if (handler is not null)
                {
                    monitor.StateChanged -= handler;
                }

                try
                {
                    monitor.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "停止注册表摄像头监控失败。");
                }
            }

            RecomputeUsage();
        }

        private void OnRegistrySnapshot(RegistryCameraActivityMonitor monitor, RegistryMonitorSnapshot snapshot)
        {
            PostToUiThread(() =>
            {
                if (!ReferenceEquals(_registryMonitor, monitor))
                {
                    return;
                }

                _registrySnapshot = snapshot;
                RegistryStatus = BuildRegistrySummary(snapshot);
                RegistryStatusDetails = BuildRegistryDetails(snapshot);
                RecomputeUsage();
            });
        }

        private static List<string> ParseRegistryKeyPaths()
        {
            var text = Plugin.Settings.RegistryKeyPathsText;
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return text
                .Split(new[] { "\r\n", "\n", "\r" },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(path => path.Trim().Trim('"').Trim())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildRegistrySummary(RegistryMonitorSnapshot snapshot)
        {
            var states = snapshot.PathStates;
            if (states.Count == 0)
            {
                return "未配置注册表项路径";
            }

            var total = states.Count;
            var inUseCount = states.Count(state => state.InUse);
            var errorCount = states.Count(state => state.HasError);

            if (total == 1)
            {
                var state = states[0];
                if (state.InUse)
                {
                    return "使用中";
                }

                return state.HasError ? "路径异常" : "未使用";
            }

            if (inUseCount > 0)
            {
                var summary = $"{total} 个路径，{inUseCount} 个正在使用";
                if (errorCount > 0)
                {
                    summary += $"，{errorCount} 个异常";
                }

                return summary;
            }

            if (errorCount > 0)
            {
                return $"{total} 个路径，{errorCount} 个异常";
            }

            return $"{total} 个路径，均未使用";
        }

        private static string BuildRegistryDetails(RegistryMonitorSnapshot snapshot)
        {
            if (snapshot.PathStates.Count == 0)
            {
                return "";
            }

            return string.Join(Environment.NewLine,
                snapshot.PathStates.Select(state =>
                {
                    var processSuffix = state.InUse && !string.IsNullOrWhiteSpace(state.ProcessName)
                        ? $"（进程：{state.ProcessName}）"
                        : "";
                    return $"{state.ConfiguredPath}：{state.Status}{processSuffix}";
                }));
        }

        private void RecomputeUsage()
        {
            var activeProcessNames = new List<string>();
            if (_nativeInUse && !string.IsNullOrWhiteSpace(_nativeProcessName))
            {
                activeProcessNames.Add(_nativeProcessName);
            }

            if (_registrySnapshot?.InUse == true)
            {
                foreach (var processName in _registrySnapshot.ActiveProcessNames)
                {
                    if (!string.IsNullOrWhiteSpace(processName))
                    {
                        activeProcessNames.Add(processName);
                    }
                }
            }

            var distinctProcessNames = activeProcessNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var inUse = _nativeInUse || _registrySnapshot?.InUse == true;
            var processNameText = distinctProcessNames.Length == 0
                ? null
                : string.Join(", ", distinctProcessNames);

            var changed = IsCameraInUse != inUse ||
                          !string.Equals(ProcessName, processNameText, StringComparison.Ordinal);

            _activeProcessNames = distinctProcessNames;
            IsCameraInUse = inUse;
            ProcessName = processNameText;

            if (!changed)
            {
                return;
            }

            UsageChanged?.Invoke(inUse);
            NotifyRulesetChanged();
        }

        private void NotifyRulesetChanged()
        {
            try
            {
                Dispatcher.UIThread.Post(() => _rulesetService.NotifyStatusChanged());
            }
            catch (InvalidOperationException)
            {
                // Dispatcher 已不可用（应用正在退出）。
            }
        }

        private static void PostToUiThread(Action action)
        {
            try
            {
                Dispatcher.UIThread.Post(action);
            }
            catch (InvalidOperationException)
            {
                // Dispatcher 已不可用（应用正在退出）。
            }
        }

        private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(Plugin.Settings.EnableMediaFoundationMonitoring):
                    if (_monitoringRequested)
                    {
                        RefreshNativeMonitoringCore();
                    }
                    else
                    {
                        UpdateMonitoringState();
                    }

                    break;
                case nameof(Plugin.Settings.EnableRegistryMonitoring):
                    if (_monitoringRequested)
                    {
                        RefreshRegistryMonitoringCore(forceRestart: true);
                    }
                    else
                    {
                        StopRegistryMonitoringCore();
                        UpdateRegistryIdleStatus();
                        UpdateMonitoringState();
                    }

                    break;
                case nameof(Plugin.Settings.RegistryKeyPathsText):
                case nameof(Plugin.Settings.RegistryPollingIntervalSeconds):
                    if (_monitoringRequested)
                    {
                        RefreshRegistryMonitoringCore(forceRestart: true);
                    }
                    else
                    {
                        UpdateRegistryIdleStatus();
                    }

                    break;
            }
        }

        private void RefreshNativeMonitoringCore()
        {
            var cameraId = CurrentCameraId;
            if (Plugin.Settings.EnableMediaFoundationMonitoring &&
                !string.IsNullOrWhiteSpace(cameraId))
            {
                if (_nativeMonitor is null)
                {
                    StartNativeMonitoringCore(cameraId);
                }
            }
            else
            {
                StopNativeMonitoringCore();
            }

            UpdateMonitoringState();
        }

        private void RefreshRegistryMonitoringCore(bool forceRestart)
        {
            if (!_monitoringRequested || !Plugin.Settings.EnableRegistryMonitoring)
            {
                StopRegistryMonitoringCore();
                UpdateRegistryIdleStatus();
                UpdateMonitoringState();
                return;
            }

            if (!forceRestart && _registryMonitor is not null)
            {
                UpdateMonitoringState();
                return;
            }

            StopRegistryMonitoringCore();
            StartRegistryMonitoringCore();
        }

        private void UpdateRegistryIdleStatus()
        {
            if (!Plugin.Settings.EnableRegistryMonitoring)
            {
                RegistryStatus = "未启用";
                RegistryStatusDetails = "";
                return;
            }

            var paths = ParseRegistryKeyPaths();
            if (paths.Count == 0)
            {
                RegistryStatus = "未配置注册表项路径";
                RegistryStatusDetails = "";
                return;
            }

            var monitor = new RegistryCameraActivityMonitor(
                paths,
                Plugin.Settings.RegistryPollingIntervalSeconds,
                _logger);
            try
            {
                var initialSnapshot = monitor.InitialSnapshot;
                if (!monitor.HasValidTargets)
                {
                    RegistryStatus = "没有可用的注册表路径";
                    RegistryStatusDetails = BuildRegistryDetails(initialSnapshot);
                    return;
                }

                RegistryStatus = _monitoringRequested ? "注册表监控未启动" : "未开始监控";
                RegistryStatusDetails = BuildIdleRegistryDetails(initialSnapshot);
            }
            finally
            {
                monitor.Dispose();
            }
        }

        private static string BuildIdleRegistryDetails(RegistryMonitorSnapshot snapshot)
        {
            return string.Join(Environment.NewLine,
                snapshot.PathStates.Select(state =>
                    $"{state.ConfiguredPath}：{(state.HasError ? state.Status : "未开始监控")}"));
        }

        private void UpdateMonitoringState()
        {
            var isMonitoring = IsMonitoring;
            var isMediaFoundationMonitoring = _nativeMonitor is not null;
            var isRegistryMonitoring = _registryMonitor is not null;
            var changed = _lastIsMonitoring != isMonitoring ||
                          IsMediaFoundationMonitoring != isMediaFoundationMonitoring ||
                          IsRegistryMonitoring != isRegistryMonitoring;

            _lastIsMonitoring = isMonitoring;
            if (IsMediaFoundationMonitoring != isMediaFoundationMonitoring)
            {
                IsMediaFoundationMonitoring = isMediaFoundationMonitoring;
            }

            if (IsRegistryMonitoring != isRegistryMonitoring)
            {
                IsRegistryMonitoring = isRegistryMonitoring;
            }

            if (!changed)
            {
                return;
            }

            OnPropertyChanged(nameof(IsMonitoring));
            MonitoringStateChanged?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Plugin.Settings.PropertyChanged -= OnSettingsPropertyChanged;
            _monitoringRequested = false;
            StopNativeMonitoringCore();
            CurrentCameraId = null;
            StopRegistryMonitoringCore();
            RegistryStatus = "已停止";
            RegistryStatusDetails = "";
            UpdateMonitoringState();
        }
    }
}
