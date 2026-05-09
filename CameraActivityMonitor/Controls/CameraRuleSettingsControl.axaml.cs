using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CameraActivityMonitor.Models;
using ClassIsland.Core.Abstractions.Controls;

namespace CameraActivityMonitor.Controls;

public partial class CameraRuleSettingsControl : RuleSettingsControlBase<CameraRuleSettings>
{
    public CameraRuleSettingsControl()
    {
        InitializeComponent();
    }
}