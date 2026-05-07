using CameraActivityMonitor.Models;

namespace CameraActivityMonitor.Services;

public interface ICameraActivityMonitorService : IDisposable
{
    public bool IsCameraInUse { get; }
    public bool IsMonitoring { get; }
    public string? CurrentCameraId { get; }

    public string? ProcessName { get; }

    Task<IReadOnlyList<CameraDeviceInfo>> GetAllCamerasAsync();

    public event Action<bool>? UsageChanged;

    public void StartMonitoring(string cameraId);
    public void StopMonitoring();

}
