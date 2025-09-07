using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Enterwell.Clients.Wpf.Notifications;
using Epoxy;
using ObjectList.Enums;
using YmmeUtil.Bridge;
using YmmeUtil.Bridge.Wrap;
using YmmeUtil.Bridge.Wrap.ViewModels;
using YmmeUtil.Common;
using YmmeUtil.Ymm4;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.ViewModels;

namespace ObjectList.ViewModel;

[ViewModel]
public partial class MainViewModel
{
	private bool _isInitializationComplete = false;

	public Command? Ready { get; set; }
	public Command? Unload { get; set; }
	public Command? ReloadCommand { get; set; }
	public Command? SelectionChangedCommand { get; set; }

	public Command? ToggleFilterMenuCommand { get; set; }
	public Command? SetStartRangeCurrentFrameCommand { get; set; }
	public Command? SetEndRangeCurrentFrameCommand { get; set; }
	public Command? SetGroupingCommand { get; set; }

	public Command? OpenCategoryFilterMenuCommand { get; set; }

	public string SearchText { get; set; } = string.Empty;

	public ObservableCollection<ObjectListItem> Items
	{
		get;
		private set;
	} = [];

	public ICollectionView? FilteredItems
	{
		get;
		private set;
	}

	public bool IsFilterMenuOpen { get; set; }

	public bool IsAllFilterSelected { get; set; } = true;
	public bool IsUnderSeekBarFilterSelected { get; set; }
	public bool IsRangeFilterSelected { get; set; }

	// 範囲フィルターは排他制御が必要 - ラジオボタンの仕様上、必ずどちらか一方が選択されている状態を保持
	public bool IsRangeFilterStrictMode { get; set; } =
		true; // デフォルトは完全に範囲内
	public bool IsRangeFilterOverlapMode { get; set; } // 初期値はfalse

	public bool IsCategoryFilterMenuOpen { get; set; }

	public bool IsCategoryFilterEnabled { get; set; }

	public bool IsFilterHighlightActive { get; set; }

	#region grouping_option

	public bool IsNoneGroupingSelected { get; set; } = true;
	public bool IsCategoryGroupingSelected { get; set; }
	public bool IsLayerGroupingSelected { get; set; }
	public bool IsGroupGroupingSelected { get; set; }
	public bool IsLockedGroupingSelected { get; set; }
	public bool IsHiddenGroupingSelected { get; set; }

	public FilterType CurrentFilterType { get; set; } =
		FilterType.All;

	public GroupingType CurrentGroupingType { get; set; } =
		GroupingType.None;

	#endregion grouping_option

	public int CurrentFrame { get; set; }
	public int RangeStartFrame { get; set; }
	public int RangeEndFrame { get; set; }
	public bool IsRangeInvalid { get; set; } = true;

	public Pile<UserControl> MainViewPile { get; } =
		Pile.Factory.Create<UserControl>();

	public Pile<FrameNumberEditor> RangeStartPile { get; } =
		Pile.Factory.Create<FrameNumberEditor>();
	public Pile<FrameNumberEditor> RangeEndPile { get; } =
		Pile.Factory.Create<FrameNumberEditor>();

	public string SceneName { get; set; } = string.Empty;
	public string SceneHz { get; set; } = string.Empty;
	public string SceneFps { get; set; } = string.Empty;
	public string SceneScreenSize { get; set; } =
		string.Empty;

	public int SceneLength { get; set; } = 100;

	public bool IsReloading { get; set; }

	public INotificationMessageManager NotifyManager { get; set; } =
		new NotificationMessageManager();

	public bool IsPluginEnabled { get; set; }

	public bool IsPluginWindowInitialized { get; set; }

	public int CurrentMainWindowIndex { get; set; }

	IDisposable? sceneSubscription;

	// パフォーマンス最適化: フィルタリングを間引いて実行するためのタイマー
	private DispatcherTimer? _filterTimer;
	private bool _needsFilterUpdate;

	DispatcherTimer? _timelineMonitorTimer;
	INotifyPropertyChanged? _lastRawTimeline;

	bool isRangeFilterChanging;
	bool _isSyncingFilterType; // ループ抑止

	// グルーピングタイプの同期フラグ（フィルタータイプと同様のパターン）
	bool _isSyncingGroupingType;

	static Version OlderYetVerified { get; } =
		AppUtil.IsDebug ? new(3, 0) : new(4, 40);
	static Version YetVerified { get; } =
		AppUtil.IsDebug ? new(4, 0) : new(4, 46); //2025-09 release

	public MainViewModel()
	{
		if (IsPluginWindowInitialized)
		{
			return;
		}

		_isInitializationComplete = false;

		LoadGroupingSettingsFromSettings();

		InitializeCommands();

		SetFilterTimer();
		SetTimelineMonitorTimer();

		ObjectListSettings.Default.PropertyChanged +=
			OnSettingsPropertyChanged;

		// 同期的にフィルター設定を初期化
		var save = ObjectListSettings.Default;
		var type = save.SelectedFilterType;

		if (!Enum.IsDefined<FilterType>(type))
		{
			type = FilterType.All;
			save.SelectedFilterType = type;
			save.Save();
		}

		// プロパティの初期値を設定に合わせて変更
		switch (type)
		{
			case FilterType.All:
				// 既に初期値がtrueなので何もしない
				break;
			case FilterType.UnderSeekBar:
				IsAllFilterSelected = false;
				IsUnderSeekBarFilterSelected = true;
				break;
			case FilterType.Range:
				IsAllFilterSelected = false;
				IsRangeFilterSelected = true;
				EnsureRangeFilterDefaults();
				break;
		}

		// 初期化完了
		_isInitializationComplete = true;
	}

	[SuppressMessage(
		"Design",
		"MA0051:Method is too long",
		Justification = "<保留中>"
	)]
	private void InitializeCommands()
	{
		Ready = Command.Factory.Create(
			InitializeApplicationAsync
		);

		Unload = Command.Factory.Create(() =>
		{
			//カテゴリフィルターが反映されない対策
			if (Ymm4Version.HasDocked)
			{
				//何もしない
				return default;
			}
			//旧バージョンむけはそのまま
			ObjectListSettings.Default.PropertyChanged -=
				OnSettingsPropertyChanged;
			return default;
		});

		ReloadCommand = Command.Factory.Create(() =>
		{
			IsReloading = true;
			if (
				TimelineUtil.TryGetTimeline(
					out var timeLine,
					CurrentMainWindowIndex
				)
			)
			{
				UpdateItems(timeLine);
				UpdateSceneInfo(timeLine);
			}
			IsReloading = false;
			return default;
		});

		SelectionChangedCommand =
			Command.Factory.Create<SelectionChangedEventArgs>(
				SelectionUpdateAsync
			);

		SetStartRangeCurrentFrameCommand =
			Command.Factory.Create(() =>
			{
				RangeStartFrame = CurrentFrame;
				return default;
			});

		SetEndRangeCurrentFrameCommand =
			Command.Factory.Create(() =>
			{
				RangeEndFrame = CurrentFrame;
				return default;
			});

		SetGroupingCommand = Command.Factory.Create<string>(
			(groupingType) =>
			{
				SetGrouping(groupingType);
				return default;
			}
		);

		OpenCategoryFilterMenuCommand =
			Command.Factory.Create(() =>
			{
				IsCategoryFilterMenuOpen = true;
				return default;
			});
	}

	async ValueTask InitializeApplicationAsync()
	{
		if (IsPluginWindowInitialized)
			return;
		await UIThread
			.InvokeAsync(() =>
			{
				SetWindowTitle();
				return default;
			})
			.ConfigureAwait(true);

		var appVer = AppVersion.Current;
		var save = ObjectListSettings.Default;

		var lastVer = save.LastSkippedVersion;
		if (
			lastVer.Major != appVer.Major
			|| lastVer.Minor > appVer.Minor
		)
		{
			//マイナーバージョンが上がっていればスキップをクリア
			save.IsSkipAppVersionCheck = false;
			save.LastSkippedVersion = appVer;
		}

		if (
			!save.IsSkipAppVersionCheck
			&& (
				appVer < OlderYetVerified
				|| appVer >= YetVerified
			)
		)
		{
			DisplayVersionWarning(appVer);
		}
		else
		{
			IsPluginEnabled = true;
			_ = await AwaitUiReadyAsync()
				.ConfigureAwait(true);
		}

		IsPluginWindowInitialized = true;
	}

	[SuppressMessage(
		"Usage",
		"MA0147:Avoid async void method for delegate"
	)]
	[SuppressMessage(
		"Concurrency",
		"PH_S034:Async Lambda Inferred to Async Void"
	)]
	[SuppressMessage(
		"Usage",
		"VSTHRD101:Avoid unsupported async delegates",
		Justification = "<保留中>"
	)]
	void DisplayVersionWarning(Version appVer)
	{
		_ = NotifyManager
			.CreateMessage()
			.Accent("#E0A030")
			.Foreground(NotifyUtil.DynamicForegroundTextHex)
			.Background(NotifyUtil.DynamicControlHex)
			.HasBadge("⚠︎")
			.HasHeader(
				"プラグインの動作確認ができていません。"
			)
			.HasMessage(
				$"このYMM4のバージョン v{appVer} での動作確認が取れていません。\nそれでも使用しますか？"
			)
			.Dismiss()
			.WithButton(
				"✔ OK",
				async button =>
				{
					button.IsEnabled = false;
					try
					{
						await AwaitUiReadyAsync()
							.ConfigureAwait(true);
					}
					catch (Exception ex)
					{
						Debug.WriteLine(
							$"Error in AwaitUiReadyAsync: {ex.Message}"
						);
						NotifyManager.Info(
							"Error",
							$"理由: {ex.Message}"
						);
						return;
					}
					button.IsEnabled = true;

					IsPluginEnabled = true;
				}
			)
			.Dismiss()
			.WithButton(
				"✖️ いいえ",
				button =>
				{
					IsPluginEnabled = false;
					NotifyManager.Info(
						"プラグインのアプデも確認してください",
						"対応バージョンがでているかもしれません。\n「ファイル」＞「設定」＞「YMM4オブジェクトリスト」＞「Update check」"
					);
				}
			)
			.WithAdditionalContent(
				ContentLocation.Bottom,
				new Border
				{
					BorderThickness = new Thickness(
						0,
						1,
						0,
						0
					),
					BorderBrush =
						NotifyUtil.GetDynamicBrush(
							SystemColors.ActiveBorderBrushKey
						),
					Child = GetBindingCheckBox(),
				}
			)
			.Queue();

		static CheckBox GetBindingCheckBox()
		{
			var cb = new CheckBox
			{
				Margin = new Thickness(12, 8, 12, 8),
				HorizontalAlignment =
					HorizontalAlignment.Left,
				Content =
					"次のバージョンまで次回からは確認しない",
				Foreground =
					NotifyUtil.DynamicForegroundTextBrush,
			};
			cb.SetBinding(
				CheckBox.IsCheckedProperty,
				new Binding(
					nameof(
						ObjectListSettings
							.Default
							.IsSkipAppVersionCheck
					)
				)
				{
					Source = ObjectListSettings.Default,
					Mode = BindingMode.TwoWay,
				}
			);
			return cb;
		}
	}

	/// <summary>
	/// YMM4のUI初期化を待機 - 最大30秒間試行
	/// </summary>
	/// <returns></returns>
	async Task<bool> AwaitUiReadyAsync()
	{
		const int maxAttempts = 60; // 30秒間試行（500ms × 60回）

		await Task.Delay(500).ConfigureAwait(false);

		for (
			int attempt = 0;
			attempt < maxAttempts;
			attempt++
		)
		{
			await UIThread.Bind();

			var foundWin = Application
				.Current.Windows.OfType<Window>()
				.FirstOrDefault(window =>
					IsRealUiWindow(window)
					&& window.IsLoaded
				);

			var hasMainVM =
				foundWin?.DataContext is IMainViewModel;

			await UIThread.Unbind();

			if (foundWin is not null && hasMainVM)
			{
				try
				{
					await UIThread
						.InvokeAsync(async () =>
						{
							await OnHostUiReadyAsync(
									foundWin
								)
								.ConfigureAwait(false);
							EnsureRangeFilterDefaults();
						})
						.ConfigureAwait(true);

					return true;
				}
				catch (Exception ex)
				{
					Debug.WriteLine(
						$"Error in OnHostUiReadyAsync: {ex.Message}"
					);
				}
			}

			await Task.Delay(500).ConfigureAwait(false);
		}

		Debug.WriteLine(
			"UI window detection timed out after 30 seconds"
		);
		NotifyManager.Warn(
			"UI初期化に失敗しました。",
			"理由：タイムアウト"
		);
		return false;
	}

	void EnsureFilterType()
	{
		var save = ObjectListSettings.Default;
		var type = save.SelectedFilterType;
		if (!Enum.IsDefined<FilterType>(type))
		{
			type = FilterType.All;
			save.SelectedFilterType = type;
			save.Save();
		}

		// Enum → UI 同期（boolは CurrentFilterType セッター内で追従）
		var was = _isInitializationComplete;
		_isInitializationComplete = false;
		CurrentFilterType = type;
		_isInitializationComplete = was;
	}

	/// <summary>
	/// 範囲フィルターのラジオボタン初期化
	/// ViewModelインスタンス再利用時のUIバインディング更新用
	/// </summary>
	private void EnsureRangeFilterDefaults()
	{
		Debug.WriteLine(
			$"EnsureRangeFilterDefaults - Before: StrictMode={IsRangeFilterStrictMode}, OverlapMode={IsRangeFilterOverlapMode}"
		);

		var wasInitialized = _isInitializationComplete;
		_isInitializationComplete = false;

		// 初期値は必ずどちらか一方のみtrue
		IsRangeFilterStrictMode = true;
		IsRangeFilterOverlapMode = false;

		_isInitializationComplete = wasInitialized;

		Debug.WriteLine(
			$"EnsureRangeFilterDefaults - After: StrictMode={IsRangeFilterStrictMode}, OverlapMode={IsRangeFilterOverlapMode}"
		);
	}

	void UpdateFilterHighlight()
	{
		// 時間フィルター、カテゴリフィルター、グルーピングのいずれかが有効な場合にハイライト
		IsFilterHighlightActive =
			(CurrentFilterType != FilterType.All)
			|| IsCategoryFilterEnabled
			|| (CurrentGroupingType != GroupingType.None);
	}

	static void SetWindowTitle()
	{
		List<dynamic> windows =
		[
			.. Application.Current.Windows,
		];
		var win = windows
			.OfType<Window>()
			.FirstOrDefault(w =>
				w.DataContext is MainViewModel
			);
		if (win is not null)
		{
			var ver = AssemblyUtil.GetVersionString(
				typeof(Ymm4ObjectListPlugin)
			);
			if (string.IsNullOrEmpty(ver))
			{
				ver = "バージョン不明";
			}
			win.Title =
				$"YMM4 オブジェクトリスト プラグイン v{ver}";
		}
	}

	ValueTask SelectionUpdateAsync(
		SelectionChangedEventArgs e
	)
	{
		if (
			!TimelineUtil.TryGetTimeline(
				out var timeLine,
				CurrentMainWindowIndex
			)
		)
		{
			return default;
		}

		var items = e.AddedItems;
		if (items is null)
			return default;

		var wItems = items
			.OfType<ObjectListItem>()
			.Select(item => item.ConvertToItemViewModel())
			.Where(item => item is not null)
			.OfType<WrapTimelineItemViewModel>()
			.ToList();

		foreach (
			ref var item in CollectionsMarshal.AsSpan(
				wItems
			)
		)
		{
			try
			{
				var cmd = item.SelectCommand;
				if (cmd?.CanExecute(null) == true)
				{
					cmd.Execute(null);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(
					$"Failed to select item: {item}, {ex.Message}"
				);
			}
		}

		return default;
	}

	// YMM4のメインUIウィンドウかどうかを判定
	static bool IsRealUiWindow(Window window)
	{
		return !string.IsNullOrWhiteSpace(window.Title)
			&& !string.Equals(
				window.Title,
				"Splash",
				StringComparison.Ordinal
			)
			&& window.DataContext is IMainViewModel;
	}

	[SuppressMessage(
		"Usage",
		"MA0004:Use Task.ConfigureAwait",
		Justification = "<保留中>"
	)]
	[SuppressMessage(
		"Usage",
		"SMA0040:Missing Using Statement",
		Justification = "<保留中>"
	)]
	async ValueTask OnHostUiReadyAsync(Window mainWindow)
	{
		await MainViewPile.RentAsync(view =>
		{
			CurrentMainWindowIndex =
				ViewModelUtil
					.GetParentViewModel(view)
					?.Index ?? 0;
			return ValueTask.CompletedTask;
		});

		if (
			!TimelineUtil.TryGetTimeline(
				out var timeLine,
				CurrentMainWindowIndex
			)
		)
		{
			return;
		}

		var raw =
			timeLine.RawTimeline as INotifyPropertyChanged;

		// 共通化した購読セットアップを利用
		SubscribeTimeline(raw, timeLine);

		// シーン切り替えも監視
		if (
			!Ymm4Version.HasDocked
			&& TimelineUtil.TryGetTimelineVmValue(
				out var timeLineVm,
				CurrentMainWindowIndex
			)
		)
		{
			// RxのSubscribeはバックグラウンドスレッドで発火する場合がある
			sceneSubscription =
				timeLineVm.SelectedScene.Subscribe(x =>
				{
					_ = UIThread
						.InvokeAsync(() =>
						{
							try
							{
								var tl = x?.Timeline;
								if (tl is null)
								{
									return default;
								}
								SubscribeTimeline(
									tl.RawTimeline
										is INotifyPropertyChanged raw
										? raw
										: null,
									tl
								);
							}
							catch (Exception ex)
							{
								Console.WriteLine(
									$"Error in SelectedScene.Subscribe: {ex.Message}"
								);
							}
							return default;
						})
						.AsTask()
						.ContinueWith(
							t =>
							{
								if (t.Exception is not null)
								{
									Debug.WriteLine(
										$"Error in SelectedScene.Subscribe: {t.Exception}"
									);
								}
							},
							TaskScheduler.Default
						);
				});
		}
		return;
	}

	/// <summary>
	/// タイムラインのPropertyChanged購読と初期化を一元化
	/// </summary>
	void SubscribeTimeline(
		INotifyPropertyChanged? raw,
		WrapTimeLine? timeLine
	)
	{
		if (!ReferenceEquals(raw, _lastRawTimeline))
		{
			UnsubscribeTimelineEvents();
			if (raw is not null && timeLine is not null)
			{
				raw.PropertyChanged += OnTimelineChanged;
				UpdateItems(timeLine);
				UpdateSceneInfo(timeLine);
			}
			_lastRawTimeline = raw;
		}
	}

	void FilterItems()
	{
		if (FilteredItems is null)
		{
			return;
		}

		FilteredItems.Filter = item =>
		{
			if (item is not ObjectListItem yourItem)
				return false;

			// テキスト検索フィルター
			var matchesSearchText =
				string.IsNullOrEmpty(SearchText)
				|| yourItem.Label.Contains(
					SearchText,
					StringComparison.OrdinalIgnoreCase
				);

			// シークバー位置フィルター
			var matchesSeekBarFilter =
				!IsUnderSeekBarFilterSelected
				|| (
					CurrentFrame >= yourItem.Frame
					&& CurrentFrame
						<= yourItem.Frame + yourItem.Length
				);

			// 範囲フィルター - 2つのモードで異なる判定ロジック
			bool matchesRangeFilter;
			if (!IsRangeFilterSelected)
			{
				matchesRangeFilter = true;
			}
			else if (IsRangeFilterStrictMode)
			{
				// 完全に範囲内モード: オブジェクト全体が指定範囲内にある
				matchesRangeFilter =
					RangeStartFrame <= yourItem.Frame
					&& RangeEndFrame
						>= yourItem.Frame + yourItem.Length;
			}
			else
			{
				// 範囲と重複モード: オブジェクトが指定範囲と少しでも重複している
				matchesRangeFilter =
					RangeStartFrame
						< yourItem.Frame + yourItem.Length
					&& RangeEndFrame > yourItem.Frame;
			}

			//カテゴリフィルター
			var matchesCategoryFilter = CheckMatchCategory(
				yourItem
			);

			return matchesSearchText
				&& matchesSeekBarFilter
				&& matchesRangeFilter
				&& matchesCategoryFilter;
		};
	}

	[SuppressMessage(
		"Design",
		"MA0051:Method is too long",
		Justification = "<保留中>"
	)]
	internal static bool CheckMatchCategory(
		ObjectListItem yourItem
	)
	{
		var settings = ObjectListSettings.Default;

		// 型リストとフィルタboolをコレクション式でペア化（インデックスは定数名で管理）
		(bool, string)[] filters =
		[
			(
				settings.IsCategoryFilterVoiceItem,
				"VoiceItem"
			),
			(settings.IsCategoryFilterTextItem, "TextItem"),
			(
				settings.IsCategoryFilterVideoItem,
				"VideoItem"
			),
			(
				settings.IsCategoryFilterAudioItem,
				"AudioItem"
			),
			(
				settings.IsCategoryFilterImageItem,
				"ImageItem"
			),
			(
				settings.IsCategoryFilterShapeItem,
				"ShapeItem"
			),
			(
				settings.IsCategoryFilterTachieItem,
				"TachieItem"
			),
			(
				settings.IsCategoryFilterTachieFaceItem,
				"TachieFaceItem"
			),
			(
				settings.IsCategoryFilterEffectItem,
				"EffectItem"
			),
			(
				settings.IsCategoryFilterTransitionItem,
				"TransitionItem"
			),
			(
				settings.IsCategoryFilterSceneItem,
				"SceneItem"
			),
			(
				settings.IsCategoryFilterFrameBufferItem,
				"FrameBufferItem"
			),
			(
				settings.IsCategoryFilterGroupItem,
				"GroupItem"
			),
		];

		return !string.IsNullOrEmpty(yourItem.Category)
			&& Array.Exists(
				filters,
				f =>
					f.Item1
					&& string.Equals(
						yourItem.RawItemCategory,
						f.Item2,
						StringComparison.Ordinal
					)
			);
	}

	void LoadGroupingSettingsFromSettings()
	{
		var savedGrouping = ObjectListSettings
			.Default
			.SelectedGroupingType;
		SetGroupingFromEnum(savedGrouping);
	}

	void SetGroupingFromEnum(GroupingType groupingType)
	{
		// すべてのグルーピング選択をリセット
		IsNoneGroupingSelected = false;
		IsCategoryGroupingSelected = false;
		IsLayerGroupingSelected = false;
		IsGroupGroupingSelected = false;
		IsLockedGroupingSelected = false;
		IsHiddenGroupingSelected = false;

		// 指定されたグルーピングを設定
		switch (groupingType)
		{
			case GroupingType.None:
				IsNoneGroupingSelected = true;
				break;
			case GroupingType.Category:
				IsCategoryGroupingSelected = true;
				break;
			case GroupingType.Layer:
				IsLayerGroupingSelected = true;
				break;
			case GroupingType.Group:
				IsGroupGroupingSelected = true;
				break;
			case GroupingType.IsLocked:
				IsLockedGroupingSelected = true;
				break;
			case GroupingType.IsHidden:
				IsHiddenGroupingSelected = true;
				break;
			default:
				IsNoneGroupingSelected = true;
				break;
		}

		// 初期化完了後のみApplyGrouping()を実行
		if (_isInitializationComplete)
		{
			ApplyGrouping();
		}
	}

	void SetGrouping(string groupingType)
	{
		// 文字列からenumへ変換
		var enumValue = groupingType switch
		{
			"None" => GroupingType.None,
			"Category" => GroupingType.Category,
			"Layer" => GroupingType.Layer,
			"Group" => GroupingType.Group,
			"IsLocked" => GroupingType.IsLocked,
			"IsHidden" => GroupingType.IsHidden,
			_ => GroupingType.None,
		};

		// 設定を保存
		ObjectListSettings.Default.SelectedGroupingType =
			enumValue;
		ObjectListSettings.Default.Save();

		// UI状態同期・ApplyGroupingは共通化
		SetGroupingFromEnum(enumValue);
	}

	void ApplyGrouping()
	{
		if (FilteredItems is null)
		{
			return;
		}

		FilteredItems.GroupDescriptions.Clear();

		if (IsCategoryGroupingSelected)
		{
			FilteredItems.GroupDescriptions.Add(
				new PropertyGroupDescription(
					nameof(ObjectListItem.Category)
				)
			);
		}
		else if (IsLayerGroupingSelected)
		{
			FilteredItems.GroupDescriptions.Add(
				new PropertyGroupDescription(
					nameof(ObjectListItem.Layer)
				)
			);
		}
		else if (IsGroupGroupingSelected)
		{
			FilteredItems.GroupDescriptions.Add(
				new PropertyGroupDescription(
					nameof(ObjectListItem.Group)
				)
			);
		}
		else if (IsLockedGroupingSelected)
		{
			FilteredItems.GroupDescriptions.Add(
				new PropertyGroupDescription(
					nameof(ObjectListItem.IsLockedLabel)
				)
			);
		}
		else if (IsHiddenGroupingSelected)
		{
			FilteredItems.GroupDescriptions.Add(
				new PropertyGroupDescription(
					nameof(ObjectListItem.IsHiddenLabel)
				)
			);
		}
	}

	[SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<保留中>")]
	async void OnTimelineChanged(
		object? sender,
		PropertyChangedEventArgs e
	)
	{
		try
		{
			var isBound = await UIThread
				.IsBoundAsync()
				.ConfigureAwait(true);

			if (isBound)
			{
				// UIスレッドなら直接処理
				try
				{
					HandleTimelineChanged(e);
				}
				catch (Exception ex)
				{
					Console.WriteLine(
						$"OnTimelineChanged error: {ex.Message}"
					);
				}
			}
			else
			{
				// UIスレッドで処理を実行
				try
				{
					await UIThread
						.InvokeAsync(() =>
						{
							try
							{
								HandleTimelineChanged(e);
							}
							catch (Exception ex)
							{
								Console.WriteLine(
									$"OnTimelineChanged error: {ex.Message}"
								);
							}
							return default;
						})
						.ConfigureAwait(true);
				}
				catch (Exception ex)
				{
					Console.WriteLine(
						$"OnTimelineChanged error: {ex.Message}"
					);
				}
			}
		}
		catch (System.Exception ex)
		{
			Debug.WriteLine(
				"OnTimelineChanged: Failed to check UI thread status: " + ex.Message
			);
		}
	}

	void HandleTimelineChanged(PropertyChangedEventArgs e)
	{
		if (
			!TimelineUtil.TryGetTimeline(
				out var timeLine,
				CurrentMainWindowIndex
			)
		)
		{
			return;
		}

		switch (e.PropertyName)
		{
			case "Items":
				UpdateItems(timeLine);
				break;
			case nameof(WrapTimeLine.CurrentFrame):
				CurrentFrame = timeLine.CurrentFrame;
				break;
			case nameof(WrapTimeLine.Length):
			case nameof(WrapTimeLine.Name):
			case nameof(WrapTimeLine.Id):
			case nameof(WrapTimeLine.MaxLayer):
				UpdateSceneInfo(timeLine);
				break;
			default:
				break;
		}
	}

	[SuppressMessage(
		"Usage",
		"SMA0040:Missing Using Statement",
		Justification = "<保留中>"
	)]
	void UpdateItems(WrapTimeLine timeLine)
	{
		if (
			!TimelineUtil.TryGetItemViewModels(
				out var itemViewModels,
				CurrentMainWindowIndex
			)
		)
		{
			return;
		}

		// 既存のイベント購読を解除
		foreach (var item in Items)
		{
			item.PropertyChanged -=
				OnObjectListItemPropertyChanged;
			item.Dispose();
		}

		Items = new ObservableCollection<ObjectListItem>(
			itemViewModels.Select(
				item => new ObjectListItem(
					item,
					timeLine.VideoInfo.FPS
				)
			)
		);
		OnItemsChanged();

		// プロジェクト読み込み時にフィルター設定を再確認
		EnsureFilterType();
	}

	void ValidateRange()
	{
		IsRangeInvalid = RangeStartFrame >= RangeEndFrame;
		FilterItems();
	}

	void OnItemsChanged()
	{
		// ObjectListItemのプロパティ変更イベントを購読
		foreach (var item in Items)
		{
			item.PropertyChanged +=
				OnObjectListItemPropertyChanged;
		}

		FilteredItems = CollectionViewSource.GetDefaultView(
			Items
		);
		FilterItems();
		ApplyGrouping();
	}

	void OnObjectListItemPropertyChanged(
		object? sender,
		PropertyChangedEventArgs e
	)
	{
		// グルーピング対象プロパティが変更された場合はCollectionViewを更新
		if (
			(
				string.Equals(
					e.PropertyName,
					nameof(ObjectListItem.IsLocked),
					StringComparison.Ordinal
				)
				|| string.Equals(
					e.PropertyName,
					nameof(ObjectListItem.IsHidden),
					StringComparison.Ordinal
				)
				|| string.Equals(
					e.PropertyName,
					nameof(ObjectListItem.IsLockedLabel),
					StringComparison.Ordinal
				)
				|| string.Equals(
					e.PropertyName,
					nameof(ObjectListItem.IsHiddenLabel),
					StringComparison.Ordinal
				)
			)
			&& (
				(
					IsLockedGroupingSelected
					&& (
						string.Equals(
							e.PropertyName,
							nameof(ObjectListItem.IsLocked),
							StringComparison.Ordinal
						)
						|| string.Equals(
							e.PropertyName,
							nameof(
								ObjectListItem.IsLockedLabel
							),
							StringComparison.Ordinal
						)
					)
				)
				|| (
					IsHiddenGroupingSelected
					&& (
						string.Equals(
							e.PropertyName,
							nameof(ObjectListItem.IsHidden),
							StringComparison.Ordinal
						)
						|| string.Equals(
							e.PropertyName,
							nameof(
								ObjectListItem.IsHiddenLabel
							),
							StringComparison.Ordinal
						)
					)
				)
			)
		)
		{
			FilteredItems?.Refresh();
		}
	}

	void UpdateSceneInfo(WrapTimeLine timeLine)
	{
		SceneName = timeLine.Name;
		SceneHz = $"{timeLine.VideoInfo.Hz} Hz";
		SceneFps = $"{timeLine.VideoInfo.FPS} FPS";
		SceneScreenSize =
			$"{timeLine.VideoInfo.Width} x {timeLine.VideoInfo.Height}";

		SceneLength = Math.Max(100, timeLine.Length);

		foreach (var item in Items)
		{
			item.FPS = timeLine.VideoInfo.FPS;
		}
	}

	[SuppressMessage(
		"Usage",
		"VSTHRD100:Avoid async void methods",
		Justification = "<保留中>"
	)]
	async void OnSettingsPropertyChanged(
		object? sender,
		PropertyChangedEventArgs e
	)
	{
		try
		{
			var isBound = await UIThread
				.IsBoundAsync()
				.ConfigureAwait(true);

			if (isBound)
			{
				// UIスレッドなら直接処理
				try
				{
					HandleSettingChanged(e);
				}
				catch (Exception ex)
				{
					Console.WriteLine(
						$"OnSettingsPropertyChanged Error: {ex.Message}"
					);
				}
			}
			else
			{
				// UIスレッドで処理を実行
				try
				{
					await UIThread
						.InvokeAsync(() =>
						{
							try
							{
								HandleSettingChanged(e);
							}
							catch (Exception ex)
							{
								Console.WriteLine(
									$"OnSettingsPropertyChanged Error: {ex.Message}"
								);
							}
							return default;
						})
						.ConfigureAwait(true);
				}
				catch (Exception ex)
				{
					Console.WriteLine(
						$"OnSettingsPropertyChanged Error: {ex.Message}"
					);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine(
				$"OnSettingsPropertyChanged Error: UIThread.IsBoundAsync failed, {ex.Message}"
			);
		}
	}

	private void HandleSettingChanged(
		PropertyChangedEventArgs e
	)
	{
		switch (e.PropertyName)
		{
			case nameof(
				ObjectListSettings.SelectedGroupingType
			):
				var groupingType = ObjectListSettings
					.Default
					.SelectedGroupingType;
				SetGroupingFromEnum(groupingType);
				ApplyGrouping();
				break;

			// カテゴリフィルターの変更
			case nameof(
				ObjectListSettings.IsCategoryFilterVoiceItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterTextItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterVideoItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterAudioItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterImageItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterShapeItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterTachieItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterTachieFaceItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterEffectItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterTransitionItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterSceneItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterFrameBufferItem
			):
			case nameof(
				ObjectListSettings.IsCategoryFilterGroupItem
			):
				FilterItems();
				CheckCategoryFilterEnabled();
				break;

			case nameof(
				ObjectListSettings.ShowLengthViewMode
			):
				ObjectListItem.ShowLengthViewMode =
					ObjectListSettings
						.Default
						.ShowLengthViewMode;
				break;

			case nameof(
				ObjectListSettings.IsShowColumnColor
			):
			case nameof(
				ObjectListSettings.IsShowColumnCategory
			):
			case nameof(
				ObjectListSettings.IsShowColumnLayer
			):
			case nameof(
				ObjectListSettings.IsShowColumnGroup
			):
			case nameof(
				ObjectListSettings.IsShowColumnFrame
			):
			case nameof(
				ObjectListSettings.IsShowColumnLength
			):
			case nameof(
				ObjectListSettings.IsShowColumnLock
			):
			case nameof(
				ObjectListSettings.IsShowColumnHidden
			):
			case nameof(ObjectListSettings.IsShowFooter):
			case nameof(
				ObjectListSettings.IsShowFooterSceneName
			):
			case nameof(
				ObjectListSettings.IsShowFooterSceneFps
			):
			case nameof(
				ObjectListSettings.IsShowFooterSceneHz
			):
			case nameof(
				ObjectListSettings.IsShowFooterSceneScreenSize
			):
			default:
				break;
		}
	}

	/// <summary>
	/// カテゴリフィルターのいずれかが有効ならtrue
	/// </summary>
	private void CheckCategoryFilterEnabled()
	{
		// IsCategoryFilter* は false の時にフィルタ有効

		IsCategoryFilterEnabled =
			!ObjectListSettings
				.Default
				.IsCategoryFilterVoiceItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterTextItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterVideoItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterAudioItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterImageItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterShapeItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterTachieItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterTachieFaceItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterEffectItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterTransitionItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterSceneItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterFrameBufferItem
			|| !ObjectListSettings
				.Default
				.IsCategoryFilterGroupItem;

		// 追加: 集約プロパティ更新
		UpdateFilterHighlight();
	}

	// パフォーマンス最適化用タイマー設定
	void SetFilterTimer()
	{
		_filterTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(100), // 100ms間隔で実行
		};
		_filterTimer.Tick += (sender, e) =>
		{
			_filterTimer.Stop();
			if (_needsFilterUpdate)
			{
				FilterItems();
				_needsFilterUpdate = false;
			}
		};
	}

	void SetTimelineMonitorTimer()
	{
		_timelineMonitorTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500), // 0.5秒ごとに監視
		};
		_timelineMonitorTimer.Tick += (sender, e) =>
		{
			try
			{
				MonitorTimelineReference();
			}
			catch (Exception ex)
			{
				_timelineMonitorTimer.Stop();
				// エラーログを出力
				Console.WriteLine(
					$"Timeline monitoring error: {ex.Message}"
				);
			}
		};
		_timelineMonitorTimer.Start();
	}

	void MonitorTimelineReference()
	{
		if (
			!IsPluginEnabled
			|| !TimelineUtil.TryGetTimeline(
				out var timeLine,
				CurrentMainWindowIndex
			)
		)
		{
			// プロジェクト未読込など
			UnsubscribeTimelineEvents();
			_lastRawTimeline = null;
			return;
		}

		var raw =
			timeLine.RawTimeline as INotifyPropertyChanged;
		SubscribeTimeline(raw, timeLine);
	}

	void UnsubscribeTimelineEvents()
	{
		if (_lastRawTimeline is not null)
		{
			_lastRawTimeline.PropertyChanged -=
				OnTimelineChanged;
		}
	}
}
