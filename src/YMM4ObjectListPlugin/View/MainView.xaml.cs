using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ObjectList.View;

/// <summary>
/// Interaction logic for UserControl1.xaml
/// </summary>
public partial class MainView : UserControl
{
	public MainView()
	{
		InitializeComponent();
	}

	private void StrictModeRadio_Loaded(
		object sender,
		RoutedEventArgs e
	)
	{
		var radio = sender as RadioButton;
		Debug.WriteLine(
			$"[UI] StrictModeRadio_Loaded: IsChecked={radio?.IsChecked}"
		);
	}

	private void OverlapModeRadio_Loaded(
		object sender,
		RoutedEventArgs e
	)
	{
		var radio = sender as RadioButton;
		Debug.WriteLine(
			$"[UI] OverlapModeRadio_Loaded: IsChecked={radio?.IsChecked}"
		);
	}
}
