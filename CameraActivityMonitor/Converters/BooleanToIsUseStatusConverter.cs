using Avalonia.Data.Converters;

namespace CameraActivityMonitor.Converters
{
    public class BooleanToIsUseStatusConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? "正在使用" : "未使用";
            }
            return "未知";
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
