using CommunityToolkit.Mvvm.ComponentModel;

namespace CameraActivityMonitor.Models
{
    /// <summary>
    /// 摄像头状态变化触发器的设置。
    /// </summary>
    public partial class CameraUsageChangedTriggerSettings : ObservableObject
    {
        /// <summary>
        /// 触发时机：0 = 任意变化，1 = 摄像头被占用时，2 = 摄像头变为空闲时。
        /// </summary>
        [ObservableProperty]
        private int _mode;
    }
}
