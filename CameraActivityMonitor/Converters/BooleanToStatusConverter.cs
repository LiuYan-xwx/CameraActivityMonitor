using Avalonia.Data.Converters;

namespace CameraActivityMonitor.Converters
{
    public class BooleanToStatusConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? "正在监控" : "不在监控";
            }
            return "未知";
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
