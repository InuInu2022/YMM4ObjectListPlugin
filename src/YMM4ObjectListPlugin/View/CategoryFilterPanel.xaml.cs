using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ObjectList.View;

/// <summary>
/// Interaction logic for CategoryFilterPanel.xaml
/// </summary>
public partial class CategoryFilterPanel : UserControl
{
	public static readonly DependencyProperty PanelStyleProperty =
		DependencyProperty.Register(
			nameof(PanelStyle),
			typeof(string),
			typeof(CategoryFilterPanel),
			new PropertyMetadata("Default")
		);

	public string PanelStyle
	{
		get => (string)GetValue(PanelStyleProperty);
		set => SetValue(PanelStyleProperty, value);
	}

	public CategoryFilterPanel()
	{
		InitializeComponent();
	}
}
