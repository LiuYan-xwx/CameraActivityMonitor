using CameraActivityMonitor.Controls;
using CameraActivityMonitor.Models;
using CameraActivityMonitor.Services;
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
            services.AddRule<CameraRuleSettings, CameraRuleSettingsControl>("cameraactivitymonitor.iscamerainuse", "摄像头是否被使用", "\uE392");

            AppBase.Current.AppStarted += (_, _) => StartCameraMonitoringIfNeeded();

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