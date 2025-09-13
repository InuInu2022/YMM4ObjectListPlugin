using System;
using System.Globalization;
using System.Windows.Data;
using ObjectList.Enums;

namespace ObjectList.View.Converters;

public sealed class GroupingTypeRadioConverter : IValueConverter
{
	public object Convert(
		object value,
		Type targetType,
		object parameter,
		CultureInfo culture
	)
	{
		if (value is null || parameter is null)
		{
			return false;
		}

		// Enum型でない場合や未定義値の場合も安全に比較
		var enumType = typeof(GroupingType);
		if (!Enum.IsDefined(enumType, value))
		{
			value = GroupingType.None; // デフォルト値にフォールバック
		}

		var paramStr = parameter.ToString();
		if (
			Enum.TryParse(
				enumType,
				paramStr,
				out var paramEnum
			)
		)
		{
			return value.Equals(paramEnum);
		}

		return false;
	}

	public object ConvertBack(
		object value,
		Type targetType,
		object parameter,
		CultureInfo culture
	)
	{
		if (
			value is true
			&& parameter is string s
			&& Enum.TryParse(s, out GroupingType p)
		)
		{
			return p;
		}

		return Binding.DoNothing;
	}
}
