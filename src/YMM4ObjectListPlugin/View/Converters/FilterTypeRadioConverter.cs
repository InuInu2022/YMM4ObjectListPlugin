using System.Globalization;
using System.Windows.Data;

namespace ObjectList.View.Converters;

// RadioButton の IsChecked ⇄ FilterType を相互変換
public class FilterTypeRadioConverter : IValueConverter
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "SMA0025:Enum System Method", Justification = "<保留中>")]
	public object Convert(
		object value,
		Type targetType,
		object parameter,
		CultureInfo culture
	)
	{
		return value is FilterType ft
			&& parameter is string p
			&& Enum.TryParse<FilterType>(
				p,
				out var param
			) && ft == param;
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "SMA0025:Enum System Method", Justification = "<保留中>")]
	public object ConvertBack(
		object value,
		Type targetType,
		object parameter,
		CultureInfo culture
	)
	{
		return value is bool b
			&& b
			&& parameter is string p
			&& Enum.TryParse<FilterType>(
				p,
				out var param
			)
			? param
			: Binding.DoNothing;
	}
}
