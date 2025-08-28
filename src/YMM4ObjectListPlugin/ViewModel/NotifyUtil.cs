using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Enterwell.Clients.Wpf.Notifications;

using YukkuriMovieMaker.Theme;

namespace ObjectList.ViewModel;

public static class NotifyUtil
{
	public static SolidColorBrush DynamicForegroundTextBrush
	{
		get => GetDynamicBrush(YMM4Colors.IconBrushKey);
	}
	public static string DynamicForegroundTextHex =>
		GetDynamicHex(
			DynamicForegroundTextBrush
		);
	public static SolidColorBrush DynamicControlBrush
	{
		get => GetDynamicBrush(
			SystemColors.ControlBrushKey
		);
	}
	public static string DynamicControlHex =>
		GetDynamicHex(DynamicControlBrush);

	public static SolidColorBrush DynamicAccentBrush
	{
		get
		{
			SolidColorBrush brush;
			if (ColorConverter.ConvertFromString("#1751C3") is not Color color)
			{
				brush = SystemColors.AccentColorBrush;
				brush.Freeze();
			}
			else
			{
				brush = new SolidColorBrush(color);
				brush.Freeze();
			}
			return SystemParameters.WindowGlassBrush
				is SolidColorBrush scb
				? scb
				: brush;
		}
	}
	public static string DynamicAccentHex => GetDynamicHex(DynamicAccentBrush);

	/// <summary>
	/// 警告メッセージ通知（2行）
	/// </summary>
	/// <param name="manager"></param>
	/// <param name="header">ヘッダーテキスト</param>
	/// <param name="message">詳細メッセージ</param>
	/// <returns></returns>
	public static INotificationMessage Warn(
		this INotificationMessageManager manager,
		string header,
		string message
	)
	{
		return manager
			.CreateMessage()
			.Accent("#E0A030")
			.Foreground(
				DynamicForegroundTextHex
			)
			.Background(
				DynamicControlHex
			)
			.HasHeader(header)
			.HasBadge("Warn")
			.HasMessage(message)
			.Dismiss()
			.WithButton("OK", _ => { })
			.Animates(true)
			.AnimationInDuration(0.35)
			.Queue();
	}

	/// <summary>
	/// 情報メッセージ通知（2行）
	/// </summary>
	/// <param name="manager"></param>
	/// <param name="header">ヘッダーテキスト</param>
	/// <param name="message">詳細メッセージ</param>
	public static INotificationMessage Info(
		this INotificationMessageManager manager,
		string header,
		string message,
		bool isDelay = false
	)
	{
		var b = manager
			.CreateMessage()
			.Accent(DynamicAccentBrush)
			.Foreground(DynamicForegroundTextHex)
			.Background(DynamicControlHex)
			.HasHeader(header)
			.HasBadge("Info")
			.HasMessage(message)
			.Animates(true)
			.AnimationInDuration(0.35)
			.Dismiss();

		return isDelay
			? b.WithDelay(TimeSpan.FromSeconds(5)).Queue()
			: b.WithButton("OK", _ => { }).Queue();
	}

	public static INotificationMessage Loading(
		this INotificationMessageManager manager,
		string header,
		string message
	)
	{
		return manager
			.CreateMessage()
			.Accent(DynamicAccentBrush)
			.Foreground(DynamicForegroundTextHex)
			.Background(DynamicControlHex)
			.HasHeader(header)
			.HasMessage(message)
			.WithOverlay(
				new ProgressBar
				{
					VerticalAlignment =
						VerticalAlignment.Bottom,
					HorizontalAlignment =
						HorizontalAlignment.Stretch,
					Height = 3,
					BorderThickness = new Thickness(0),
					Foreground = DynamicAccentBrush,
					Background = Brushes.Transparent,
					IsIndeterminate = true,
					IsHitTestVisible = false,
				}
			)
			.Animates(true)
			.AnimationInDuration(0.35)
			.Queue();
	}

	public static SolidColorBrush GetDynamicBrush(ResourceKey key)
	{
		return
			Application.Current.FindResource(key)
				is SolidColorBrush brush
			? brush
			: SystemColors.ControlBrush;
	}

	public static string GetDynamicHex(
		SolidColorBrush brush
	) =>
		brush.Color.ToString(
			System
				.Globalization
				.CultureInfo
				.InvariantCulture
		);
}
