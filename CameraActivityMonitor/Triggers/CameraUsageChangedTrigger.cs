using CameraActivityMonitor.Models;
using CameraActivityMonitor.Services;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;

namespace CameraActivityMonitor.Triggers
{
    /// <summary>
    /// 当摄像头使用状态发生变化时触发。
    /// </summary>
    [TriggerInfo("cameraactivitymonitor.usageChanged", "摄像头状态变化时", "\uE392")]
    public class CameraUsageChangedTrigger(ICameraActivityMonitorService monitorService)
        : TriggerBase<CameraUsageChangedTriggerSettings>
    {
        public override void Loaded()
        {
            EnsureMonitoringStarted();
            monitorService.UsageChanged += OnUsageChanged;
        }

        public override void UnLoaded()
        {
            monitorService.UsageChanged -= OnUsageChanged;
        }

        /// <summary>
        /// 保证监控已启动，否则触发器永远收不到状态变化。
        /// </summary>
        private void EnsureMonitoringStarted()
        {
            if (monitorService.IsMonitoring)
            {
                return;
            }

            if (Plugin.Settings.SelectedCamera is { } camera)
            {
                monitorService.StartMonitoring(camera.Id);
            }
        }

        private void OnUsageChanged(bool inUse)
        {
            var matches = Settings.Mode switch
            {
                1 => inUse,
                2 => !inUse,
                _ => true
            };

            if (matches)
            {
                Trigger();
            }
        }
    }
}
