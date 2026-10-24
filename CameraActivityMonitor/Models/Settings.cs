using CommunityToolkit.Mvvm.ComponentModel;

namespace CameraActivityMonitor.Models
{
    public partial class Settings : ObservableObject
    {
        [ObservableProperty]
        private CameraDeviceInfo? _selectedCamera;

        [ObservableProperty]
        private bool _autoStart = true;

        [ObservableProperty]
        private bool _enableMediaFoundationMonitoring = true;

        [ObservableProperty]
        private bool _enableRegistryMonitoring;

        // 每行一个路径，便于用户直接粘贴多个 CapabilityAccessManager 项。
        [ObservableProperty]
        private string _registryKeyPathsText = "";

        [ObservableProperty]
        private int _registryPollingIntervalSeconds = 2;
    }
}
