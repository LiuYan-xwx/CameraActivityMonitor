using CameraActivityMonitor.Controls;
using CameraActivityMonitor.Models;
using CameraActivityMonitor.Services;
using CameraActivityMonitor.Triggers;
using CameraActivityMonitor.ViewModels;
using CameraActivityMonitor.Views;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Windows.Win32;

namespace CameraActivityMonitor
{
    [PluginEntrance]
    public class Plugin : PluginBase
    {
        public static Settings Settings { get; set; } = new Settings();

        public override async void Initialize(HostBuilderContext context, IServiceCollection services)
        {
            Settings = ConfigureFileHelper.LoadConfig<Settings>(Path.Combine(PluginConfigFolder, "Settings.json"));  // 加载配置文件
            Settings.PropertyChanged += (_, _) =>
            {
                ConfigureFileHelper.SaveConfig<Settings>(Path.Combine(PluginConfigFolder, "Settings.json"), Settings);  // 保存配置文件
            };

            PInvoke.MFStartup(PInvoke.MF_VERSION, 0);
            AppBase.Current.AppStopping += (_, _) =>
            {
                PInvoke.MFShutdown();
            };

            services.AddSingleton<ICameraActivityMonitorService, CameraActivityMonitorService>();
            services.AddSettingsPage<CameraActivityMonitorSettingsPage>();
            services.AddSingleton<SettingsPageViewModel>();
            // 规则处理程序必须在这里注册，不能等到监控服务被实例化时才注册，
            // 否则在服务创建前求值规则会返回 false 并产生“处理程序没有注册”的警告。
            services.AddRule<CameraRuleSettings, CameraRuleSettingsControl>(
                "cameraactivitymonitor.iscamerainuse", "摄像头是否被使用", "\uE392", HandleCameraRule);
            services.AddTrigger<CameraUsageChangedTrigger, CameraUsageTriggerSettingsControl>();

            AppBase.Current.AppStarted += (_, _) => StartCameraMonitoringIfNeeded();

        }

        private static bool HandleCameraRule(object? settings)
        {
            var service = IAppHost.TryGetService<ICameraActivityMonitorService>();
            if (service is null || !service.IsCameraInUse)
            {
                return false;
            }

            if (settings is CameraRuleSettings ruleSettings && ruleSettings.MatchProcessName)
            {
                if (string.IsNullOrWhiteSpace(ruleSettings.ProcessName))
                {
                    return false;
                }

                return string.Equals(service.ProcessName, ruleSettings.ProcessName, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }
        private static void StartCameraMonitoringIfNeeded()
        {
            if (Settings.AutoStart && Settings.SelectedCamera is not null)
            {
                var cameraActivityMonitorService = IAppHost.GetService<ICameraActivityMonitorService>();
                cameraActivityMonitorService.StartMonitoring(Settings.SelectedCamera.Id);
            }
        }
    }
}