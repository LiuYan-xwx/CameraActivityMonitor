using CameraActivityMonitor.Models;

namespace CameraActivityMonitor.Services;

public interface ICameraActivityMonitorService : IDisposable
{
    public bool IsCameraInUse { get; }
    public bool IsMonitoring { get; }
    public bool IsMediaFoundationMonitoring { get; }
    public bool IsRegistryMonitoring { get; }
    public string? CurrentCameraId { get; }

    public string? ProcessName { get; }
    public string? RegistryStatus { get; }
    public string? RegistryStatusDetails { get; }

    Task<IReadOnlyList<CameraDeviceInfo>> GetAllCamerasAsync();

    public event Action<bool>? UsageChanged;
    public event Action? MonitoringStateChanged;

    public void StartMonitoring(string? cameraId);
    public void StopMonitoring();
    public void StopMediaFoundationMonitoring();

}
