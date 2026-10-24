using CameraActivityMonitor.Models;
using CameraActivityMonitor.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CameraActivityMonitor.ViewModels
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        public ICameraActivityMonitorService CameraActivityMonitorService { get; }
        public bool IsMonitoring => CameraActivityMonitorService.IsMonitoring;
        public ObservableCollection<CameraDeviceInfo> Cameras { get; } = [];

        public bool IsCameraInUse => CameraActivityMonitorService.IsCameraInUse;

        public static Settings Settings => Plugin.Settings;

        public bool CanStartMonitor => !IsMonitoring;

        public CameraDeviceInfo? SelectedCamera
        {
            get => Plugin.Settings.SelectedCamera;
            set
            {
                if (SetProperty(Plugin.Settings.SelectedCamera,
                                value,
                                Plugin.Settings,
                                (s, v) => s.SelectedCamera = v))
                {
                    OnSelectedCameraChanged(value);
                }
            }
        }


        public SettingsPageViewModel(ICameraActivityMonitorService cameraActivityMonitorService)
        {
            CameraActivityMonitorService = cameraActivityMonitorService;
            CameraActivityMonitorService.UsageChanged += OnUsageChanged;
            CameraActivityMonitorService.MonitoringStateChanged += OnMonitoringStateChanged;

            if (Settings.AutoStart)
            {
                StartMonitor();
            }
        }

        private void OnUsageChanged(bool inUse)
        {
            OnPropertyChanged(nameof(IsCameraInUse));
        }

        private void OnMonitoringStateChanged()
        {
            RefreshMonitoringState();
        }


        private void OnSelectedCameraChanged(CameraDeviceInfo? value)
        {
            if (IsMonitoring == false)
            {
                return;
            }

            StartMonitor();
        }

        public async Task InitializeAsync()
        {
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            try
            {
                var devices = await CameraActivityMonitorService.GetAllCamerasAsync();
                var previousId = SelectedCamera?.Id;
                Cameras.Clear();

                foreach (var device in devices)
                {
                    Cameras.Add(device);
                }

                if (Cameras.Count == 0)
                {
                    SelectedCamera = null;
                    CameraActivityMonitorService.StopMediaFoundationMonitoring();
                    RefreshMonitoringState();
                    return;
                }

                SelectedCamera = Cameras.FirstOrDefault(x => x.Id == previousId) ?? Cameras[0];
            }
            catch
            {
                // 摄像头枚举失败时只停止 Media Foundation 监控，不能连带停止注册表监控。
                CameraActivityMonitorService.StopMediaFoundationMonitoring();
                RefreshMonitoringState();
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartMonitor))]
        private void StartMonitor()
        {
            CameraActivityMonitorService.StartMonitoring(SelectedCamera?.Id);
            RefreshMonitoringState();
        }

        [RelayCommand(CanExecute = nameof(IsMonitoring))]
        private void StopMonitor()
        {
            if (!IsMonitoring)
            {
                return;
            }

            CameraActivityMonitorService.StopMonitoring();
            RefreshMonitoringState();
        }

        private void RefreshMonitoringState()
        {
            OnPropertyChanged(nameof(IsMonitoring));
            OnPropertyChanged(nameof(IsCameraInUse));
            OnPropertyChanged(nameof(CanStartMonitor));
            StartMonitorCommand.NotifyCanExecuteChanged();
            StopMonitorCommand.NotifyCanExecuteChanged();
        }
    }
}
