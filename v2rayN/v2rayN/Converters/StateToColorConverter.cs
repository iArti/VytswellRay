using System.Globalization;
using System.Windows.Media;
using v2rayN.Common;

namespace v2rayN.Converters;

public class StateToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush Grey   = new(Color.FromRgb(120, 120, 120));
    private static readonly SolidColorBrush Amber  = new(Color.FromRgb(255, 170, 0));
    private static readonly SolidColorBrush Green  = new(Color.FromRgb(46, 204, 113));
    private static readonly SolidColorBrush Red    = new(Color.FromRgb(231, 76, 60));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ConnectionState.Connected                                  => Green,
        ConnectionState.Connecting or ConnectionState.Disconnecting => Amber,
        ConnectionState.Error                                      => Red,
        _                                                          => Grey,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
