using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CameraActivityMonitor.Controls;

public partial class CheckOrDismiss : UserControl
{
    public CheckOrDismiss()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Status StyledProperty definition
    /// </summary>
    public static readonly StyledProperty<bool> StatusProperty =
        AvaloniaProperty.Register<CheckOrDismiss, bool>(nameof(Status), false);

    /// <summary>
    /// Gets or sets the Status property. This StyledProperty 
    /// indicates The icon is checkmark or dismiss mark.
    /// </summary>
    public bool Status
    {
        get => this.GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}