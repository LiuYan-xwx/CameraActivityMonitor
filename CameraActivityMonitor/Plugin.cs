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
using System.Diagnostics;
using Windows.Win32;

namespace CameraActivityMonitor
{
    [PluginEntrance]
    public class Plugin : PluginBase
    {
        public static Settings Settings { get; set; } = new Settings();

        public override void Initialize(HostBuilderContext context, IServiceCollection services)
        {
            Settings = ConfigureFileHelper.LoadConfig<Settings>(Path.Combine(PluginConfigFolder, "Settings.json"));  // 加载配置文件
            Settings.PropertyChanged += (_, _) =>
            {
                ConfigureFileHelper.SaveConfig<Settings>(Path.Combine(PluginConfigFolder, "Settings.json"), Settings);  // 保存配置文件
            };

            PInvoke.MFStartup(PInvoke.MF_VERSION, 0);
            AppBase.Current.AppStopping += (_, _) =>
            {
                try
                {
                    // 必须在 MFShutdown 前停止 Media Foundation 监控并释放注册表轮询资源。
                    IAppHost.TryGetService<ICameraActivityMonitorService>()?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"释放摄像头监控服务失败：{ex}");
                }
                finally
                {
                    PInvoke.MFShutdown();
                }
            };

            services.AddSingleton<ICameraActivityMonitorService, CameraActivityMonitorService>();
            services.AddSettingsPage<CameraActivityMonitorSettingsPage>();
            services.AddSingleton<SettingsPageViewModel>();
            services.AddRule<CameraRuleSettings, CameraRuleSettingsControl>("cameraactivitymonitor.iscamerainuse", "摄像头是否被使用", "\uE392");

            AppBase.Current.AppStarted += (_, _) => StartCameraMonitoringIfNeeded();

        }

        private static void StartCameraMonitoringIfNeeded()
        {
            if (!Settings.AutoStart)
            {
                return;
            }

            var cameraActivityMonitorService = IAppHost.GetService<ICameraActivityMonitorService>();
            cameraActivityMonitorService.StartMonitoring(Settings.SelectedCamera?.Id);
        }
    }
}