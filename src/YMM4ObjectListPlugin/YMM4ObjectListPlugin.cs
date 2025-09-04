using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NLog.Extensions.Logging;
using ObjectList.View;
using ObjectList.ViewModel;
using YmmeUtil.Bridge;
using YmmeUtil.Ymm4;
using YukkuriMovieMaker.Plugin;
using NLog.Config;
using System.IO;
using YukkuriMovieMaker.Commons;
using NLog.Targets;
using NLog.Targets.Wrappers;
using NLog;

namespace ObjectList;

[PluginDetails(
	AuthorName = "InuInu",
	ContentId = "nc424814"
)]
public class Ymm4ObjectListPlugin : IToolPlugin, IDisposable
{
	public string Name { get; } = "YMM4 オブジェクトリスト";
	public PluginDetailsAttribute Details =>
		GetType()
			.GetCustomAttribute<PluginDetailsAttribute>()
		?? new();

	public Type ViewModelType { get; } =
		typeof(MainViewModel);
	public Type ViewType { get; } = typeof(MainView);

	private static readonly JsonSerializerOptions _jsonOptions =
		new() { WriteIndented = true };
	private readonly PluginErrorHandler _monitor;

	static readonly DisposeCollector disposer = new();
	private bool _disposedValue;

	public Ymm4ObjectListPlugin()
	{
		//DI
		SetLogging();
		var services = new ServiceCollection();
		services.AddLogging(builder =>
		{
			builder.ClearProviders();
			builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
			builder.AddNLog();
		});
		services.AddTransient<Ymm4ObjectListPlugin>();
		using var provider = services.BuildServiceProvider();

		//プラグイン由来のエラー処理
		var logger = provider.GetRequiredService<
			ILogger<Ymm4ObjectListPlugin>
		>();
		_monitor = PluginErrorHandler.Initialize(
			typeof(Ymm4ObjectListPlugin).Assembly,
			logger
		);

		//UI load
		var timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500),
		};

		void TickEvent(object? s, EventArgs e)
		{
			foreach (
				Window win in Application.Current.Windows
			)
			{
				// スプラッシュではなく、実ウィンドウかを判定
				if (IsRealUiWindow(win) && win.IsLoaded)
				{
					timer.Stop();
					OnUiReady(win);
					timer.Tick -= TickEvent;
					return;
				}
			}
		}
		timer.Tick += TickEvent;

		timer.Start();
	}

	static void SetLogging()
	{
		var config = new LoggingConfiguration();

		// ファイルターゲットの設定
		var logDir = Path.Combine(
			AppDirectories.LogDirectory,
			"Ymm4ObjectListPlugin",
			"logs"
		);
		Directory.CreateDirectory(logDir);

		var fileTarget = new FileTarget("logfile")
		{
			FileName = $"{logDir}/app_${{shortdate}}.log",
			ArchiveFileName =
				$"{logDir}/archive/app.log",
			ArchiveEvery = FileArchivePeriod.Day,
			ArchiveSuffixFormat = "_{#}",
			MaxArchiveFiles = 30,
			KeepFileOpen = false,
			Encoding = System.Text.Encoding.UTF8,
			Layout =
				"${longdate} [${level:uppercase=true}] [${logger}] ${message} ${exception:format=tostring}",
		};
		disposer.Collect(fileTarget);

		// 非同期ラッパー
		var asyncFileTarget = new AsyncTargetWrapper(
			fileTarget,
			5000,
			AsyncTargetWrapperOverflowAction.Discard
		)
		{
			Name = "asyncFile",
		};
		disposer.Collect(asyncFileTarget);

		config.AddTarget(asyncFileTarget);

		// ルールの追加
		config.AddRule(
			NLog.LogLevel.Error,
			NLog.LogLevel.Fatal,
			asyncFileTarget
		);

		// 設定を適用
		LogManager.Configuration = config;
	}

	private static bool IsRealUiWindow(Window window)
	{
		return !string.IsNullOrWhiteSpace(window.Title)
			&& !string.Equals(
				window.Title,
				"Splash",
				StringComparison.Ordinal
			);
	}

	private static void OnUiReady(Window mainWindow)
	{
		// 本体UIにアクセス可能
		//test
		var hasTL = TimelineUtil.TryGetTimeline(
			out var timeLine
		);
		if (!hasTL || timeLine is null)
			return;
		Debug.WriteLine(
			JsonSerializer.Serialize(
				timeLine.VideoInfo,
				_jsonOptions
			)
		);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				disposer.DisposeAndClear();
			}

			_disposedValue = true;
		}
	}

	// // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
	// ~Ymm4ObjectListPlugin()
	// {
	//     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
	//     Dispose(disposing: false);
	// }

	public void Dispose()
	{
		// このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
