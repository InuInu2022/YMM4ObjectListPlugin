using System;
using System.Globalization;
using System.Windows.Data;
using ObjectList.ViewModel;

namespace ObjectList.View.Converters;

public class GroupingTypeRadioConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is GroupingType gt && parameter is string s && Enum.TryParse<GroupingType>(s, out var p))
            return gt == p;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string s && Enum.TryParse<GroupingType>(s, out var p))
            return p;
        return Binding.DoNothing;
    }
}