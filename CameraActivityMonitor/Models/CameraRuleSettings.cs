using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CameraActivityMonitor.Models
{
    public partial class CameraRuleSettings : ObservableObject
    {
        [ObservableProperty]
        private bool _matchProcessName;

        [ObservableProperty]
        private string? _processName;
    }
}
