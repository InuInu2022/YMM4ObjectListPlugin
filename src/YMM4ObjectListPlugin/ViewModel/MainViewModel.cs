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

using Epoxy;

using YmmeUtil.Bridge;
using YmmeUtil.Bridge.Wrap;
using YmmeUtil.Bridge.Wrap.ViewModels;
using YmmeUtil.Common;

using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.ViewModels;

namespace ObjectList.ViewModel;

[ViewModel]
public class MainViewModel
{
	public Command? Ready { get; set; }
	public Command? ReloadCommand { get; set; }
	public Command? SelectionChangedCommand { get; set; }

	public Command? ToggleFilterMenuCommand { get; set; }
	public Command? SetStartRangeCurrentFrameCommand { get; set; }
	public Command? SetEndRangeCurrentFrameCommand { get; set; }
	public Command? SetGroupingCommand { get; set; }

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

	// フィルター選択状態
	public bool IsAllFilterSelected { get; set; } = true;
	public bool IsUnderSeekBarFilterSelected { get; set; }
	public bool IsRangeFilterSelected { get; set; }

	// グルーピング選択状態
	#region grouping_option

	public bool IsNoneGroupingSelected { get; set; } = true;
	public bool IsCategoryGroupingSelected { get; set; }
	public bool IsLayerGroupingSelected { get; set; }
	public bool IsGroupGroupingSelected { get; set; }
	public bool IsLockedGroupingSelected { get; set; }
	public bool IsHiddenGroupingSelected { get; set; }

	#endregion grouping_option

	public int CurrentFrame { get; set; }
	public int RangeStartFrame { get; set; }
	public int RangeEndFrame { get; set; }
	public bool IsRangeInvalid { get; set; } = true;

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

	IDisposable? sceneSubscription;

	private DispatcherTimer? _filterTimer;
	private bool _needsFilterUpdate;

	public MainViewModel()
	{
		Ready = Command.Factory.Create(
			InitializeApplicationAsync
		);

		ReloadCommand = Command.Factory.Create(() =>
		{
			if (
				TimelineUtil.TryGetTimeline(
					out var timeLine
				) && timeLine is not null
			)
			{
				UpdateItems(timeLine);
				UpdateSceneInfo(timeLine);
			}

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

		SetFilterTimer();
	}

	void SetFilterTimer()
	{
		// フィルタ更新用のタイマーを初期化
		_filterTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(100), // 100ms間隔で更新
		};
		_filterTimer.Tick += (s, e) =>
		{
			if (
				_needsFilterUpdate
				&& IsUnderSeekBarFilterSelected
			)
			{
				FilterItems();
				_needsFilterUpdate = false;
			}
			_filterTimer.Stop();
		};
	}

	ValueTask InitializeApplicationAsync()
	{
		//set Title
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

		// App loaded event
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
					OnHostUiReady(win);
					timer.Tick -= TickEvent;
					return;
				}
			}
		}
		timer.Tick += TickEvent;
		timer.Start();

		return default;
	}

	static ValueTask SelectionUpdateAsync(
		SelectionChangedEventArgs e
	)
	{
		if (
			!TimelineUtil.TryGetTimeline(out var timeLine)
			|| timeLine is null
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
			catch (System.Exception ex)
			{
				Debug.WriteLine(
					$"Failed to select item: {item}, {ex.Message}"
				);
			}
		}

		return default;
	}

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

	void OnHostUiReady(Window mainWindow)
	{
		var hasTL = TimelineUtil.TryGetTimeline(
			out var timeLine
		);

		if (!hasTL || timeLine is null)
			return;

		var raw = timeLine.RawTimeline;

		if (raw is INotifyPropertyChanged target)
		{
			//監視する
			target.PropertyChanged += OnTimelineChanged;
			// ここでタイムラインの変更を反映させる
			UpdateItems(timeLine);
			UpdateSceneInfo(timeLine);
		}

		var hasSceneVm = TimelineUtil.TryGetTimelineVmValue(
			out var timeLineVm
		);
		if (hasSceneVm && timeLineVm is not null)
		{
			sceneSubscription =
				timeLineVm.SelectedScene.Subscribe(x =>
				{
					var tl = x?.Timeline;
					if (tl is not null)
					{
						UpdateItems(tl);
						UpdateSceneInfo(tl);
					}
				});
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

			// レンジフィルター
			var matchesRangeFilter =
				!IsRangeFilterSelected
				|| (
					RangeStartFrame <= yourItem.Frame
					&& RangeEndFrame
						>= yourItem.Frame + yourItem.Length
				);

			// 両方の条件を満たす必要がある
			return matchesSearchText
				&& matchesSeekBarFilter
				&& matchesRangeFilter;
		};
	}

	void SetGrouping(string groupingType)
	{
		// すべてのグルーピング選択をリセット
		IsNoneGroupingSelected = false;
		IsCategoryGroupingSelected = false;
		IsLayerGroupingSelected = false;
		IsGroupGroupingSelected = false;
		IsLockedGroupingSelected = false;
		IsHiddenGroupingSelected = false;

		// 選択されたグルーピングを設定
		switch (groupingType)
		{
			case "None":
				IsNoneGroupingSelected = true;
				break;
			case "Category":
				IsCategoryGroupingSelected = true;
				break;
			case "Layer":
				IsLayerGroupingSelected = true;
				break;
			case "Group":
				IsGroupGroupingSelected = true;
				break;
			case "IsLocked":
				IsLockedGroupingSelected = true;
				break;
			case "IsHidden":
				IsHiddenGroupingSelected = true;
				break;
			default:
				IsNoneGroupingSelected = true;
				Debug.Assert(
					true,
					$"Unknown grouping type: {groupingType}"
				);
				break;
		}

		ApplyGrouping();
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
		// IsNoneGroupingSelectedの場合は何もしない（グルーピングなし）
	}

	void OnTimelineChanged(
		object? sender,
		PropertyChangedEventArgs e
	)
	{
		//Debug.WriteLine($"Property changed: {e.PropertyName}");

		if (
			!TimelineUtil.TryGetTimeline(out var timeLine)
			|| timeLine is null
		)
			return;

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

	void UpdateItems(WrapTimeLine timeLine)
	{
		if (
			!TimelineUtil.TryGetItemViewModels(
				out var itemViewModels
			) || itemViewModels is null
		)
		{
			return;
		}
		// 既存のイベント購読を解除
		foreach (var item in Items)
		{
			item.PropertyChanged -=
				OnObjectListItemPropertyChanged;
		}

		// アイテムビューのモデルが取得できた場合は、アイテムを更新する
		Items = new ObservableCollection<ObjectListItem>(
			itemViewModels.Select(
				item => new ObjectListItem(item)
			)
		);
		OnItemsChanged();
	}

	[PropertyChanged(nameof(SearchText))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask SearchTextChangedAsync(string value)
	{
		FilterItems();
		return default;
	}

	[PropertyChanged(nameof(CurrentFrame))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask CurrentFrameChangedAsync(int value)
	{
		//オプション有効時にフィルタかける（間引き処理）
		if (IsUnderSeekBarFilterSelected)
		{
			_needsFilterUpdate = true;
			if (_filterTimer?.IsEnabled != true)
			{
				_filterTimer?.Start();
			}
		}
		return default;
	}

	[PropertyChanged(nameof(IsUnderSeekBarFilterSelected))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsUnderSeekBarFilterSelectedChangedAsync(
		bool value
	)
	{
		// タイマーを使って確実にフィルタを実行
		_needsFilterUpdate = true;
		if (_filterTimer?.IsEnabled != true)
		{
			_filterTimer?.Start();
		}

		// さらに、即座にも実行（二重実行防止のためタイマー内で_needsFilterUpdateをチェック）
		if (
			value
			&& TimelineUtil.TryGetTimeline(out var timeLine)
			&& timeLine is not null
		)
		{
			CurrentFrame = timeLine.CurrentFrame;
			FilterItems();
			_needsFilterUpdate = false; // 即座に実行したのでタイマー実行を防ぐ
		}

		return default;
	}

	[PropertyChanged(nameof(IsRangeFilterSelected))]
	[SuppressMessage("", "IDE0051")]
	[SuppressMessage(
		"Usage",
		"MA0004:Use Task.ConfigureAwait",
		Justification = "<保留中>"
	)]
	private async ValueTask IsRangeFilterSelectedChangedAsync(
		bool value
	)
	{
		if (value)
		{
			// Rangeフィルタが選択された場合の処理
			var isSuccess = ItemEditorUtil.TryGetItemEditor(
				out var itemEditor
			);
			if (!isSuccess || itemEditor is null)
			{
				return;
			}
			await RangeStartPile.RentAsync(editor =>
			{
				editor.SetEditorInfo(itemEditor.EditorInfo);
				return ValueTask.CompletedTask;
			});
			await RangeEndPile.RentAsync(editor =>
			{
				editor.SetEditorInfo(itemEditor.EditorInfo);
				return ValueTask.CompletedTask;
			});
		}
	}

	[PropertyChanged(nameof(RangeStartFrame))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask RangeStartFrameChangedAsync(int value)
	{
		//開始フレーム
		ValidateRange();
		return default;
	}

	[PropertyChanged(nameof(RangeEndFrame))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask RangeEndFrameChangedAsync(int value)
	{
		//終了フレーム
		ValidateRange();
		return default;
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
		// IsLockedやIsHiddenが変更された場合、CollectionViewを更新
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
			_ = Application.Current.Dispatcher.BeginInvoke(
				() => FilteredItems?.Refresh()
			);
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
	}
}
