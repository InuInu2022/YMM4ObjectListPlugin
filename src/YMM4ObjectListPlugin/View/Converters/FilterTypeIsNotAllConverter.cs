using System.Globalization;
using System.Windows.Data;
using ObjectList.ViewModel;

namespace ObjectList.View.Converters;

public sealed class FilterTypeIsNotAllConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is FilterType ft && ft != FilterType.All;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}