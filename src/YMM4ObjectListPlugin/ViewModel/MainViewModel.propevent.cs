using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Epoxy;

using YmmeUtil.Bridge;

namespace ObjectList.ViewModel;

public partial class MainViewModel
{

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
		// シークバーフィルター有効時のパフォーマンス最適化: フィルタリングを間引き処理
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

	[PropertyChanged(nameof(IsAllFilterSelected))]
	[SuppressMessage("", "IDE0051")]
	ValueTask IsAllFilterSelectedChangedAsync(bool value)
	{
		if (_isSyncingFilterType)
			return default;
		if (value)
			CurrentFilterType = FilterType.All;
		return default;
	}

	[PropertyChanged(nameof(IsUnderSeekBarFilterSelected))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsUnderSeekBarFilterSelectedChangedAsync(
		bool value
	)
	{
		_needsFilterUpdate = true;
		if (_filterTimer?.IsEnabled != true)
		{
			_filterTimer?.Start();
		}

		// 即座にも実行（二重実行防止のためタイマー内で_needsFilterUpdateをチェック）
		if (
			value
			&& TimelineUtil.TryGetTimeline(out var timeLine)
			&& timeLine is not null
		)
		{
			CurrentFrame = timeLine.CurrentFrame;
			FilterItems();
			_needsFilterUpdate = false;
		}

		if (_isSyncingFilterType)
			return default;
		if (value)
			CurrentFilterType = FilterType.UnderSeekBar;
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
			// 範囲指定用のフレーム番号エディターを初期化
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

			// ラジオボタンの排他制御: 両方falseの状態を防ぐ
			if (
				!IsRangeFilterStrictMode
				&& !IsRangeFilterOverlapMode
			)
			{
				IsRangeFilterStrictMode = true;
			}

			if (_isSyncingFilterType)
				return;
			CurrentFilterType = FilterType.Range;
		}

		FilterItems();
	}

	[PropertyChanged(nameof(IsRangeFilterStrictMode))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsRangeFilterStrictModeChangedAsync(
		bool value
	)
	{
		if (
			!_isInitializationComplete
			|| isRangeFilterChanging
		)
		{
			return default;
		}

		isRangeFilterChanging = true;

		if (value)
		{
			IsRangeFilterOverlapMode = false;
		}
		else if (!IsRangeFilterOverlapMode)
		{
			IsRangeFilterOverlapMode = true;
		}

		isRangeFilterChanging = false;

		FilterItems();
		return default;
	}

	[PropertyChanged(nameof(IsRangeFilterOverlapMode))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask IsRangeFilterOverlapModeChangedAsync(
		bool value
	)
	{
		if (
			!_isInitializationComplete
			|| isRangeFilterChanging
		)
		{
			return default;
		}

		isRangeFilterChanging = true;

		if (value)
		{
			IsRangeFilterStrictMode = false;
		}
		else if (!IsRangeFilterStrictMode)
		{
			IsRangeFilterStrictMode = true;
		}

		isRangeFilterChanging = false;

		FilterItems();
		return default;
	}

	[PropertyChanged(nameof(RangeStartFrame))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask RangeStartFrameChangedAsync(int value)
	{
		ValidateRange();
		return default;
	}

	[PropertyChanged(nameof(RangeEndFrame))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask RangeEndFrameChangedAsync(int value)
	{
		ValidateRange();
		return default;
	}

	[PropertyChanged(nameof(IsNoneGroupingSelected))]
	[SuppressMessage("", "IDE0051")]
	ValueTask IsNoneGroupingSelectedChangedAsync(
		bool v
	)
	{
		if (!_isSyncingGroupingType && v)
			CurrentGroupingType = GroupingType.None;
		return default;
	}

	[PropertyChanged(nameof(IsCategoryGroupingSelected))]
	[SuppressMessage("", "IDE0051")]
	ValueTask IsCategoryGroupingSelectedChangedAsync(
		bool v
	)
	{
		if (!_isSyncingGroupingType && v)
			CurrentGroupingType = GroupingType.Category;
		return default;
	}

	[PropertyChanged(nameof(IsLayerGroupingSelected))]
	[SuppressMessage("", "IDE0051")]
	ValueTask IsLayerGroupingSelectedChangedAsync(
		bool v
	)
	{
		if (!_isSyncingGroupingType && v)
			CurrentGroupingType = GroupingType.Layer;
		return default;
	}

	[PropertyChanged(nameof(IsGroupGroupingSelected))]
	[SuppressMessage("", "IDE0051")]
	ValueTask IsGroupGroupingSelectedChangedAsync(
		bool v
	)
	{
		if (!_isSyncingGroupingType && v)
			CurrentGroupingType = GroupingType.Group;
		return default;
	}

	[PropertyChanged(nameof(IsLockedGroupingSelected))]
	[SuppressMessage("", "IDE0051")]
	ValueTask IsLockedGroupingSelectedChangedAsync(
		bool v
	)
	{
		if (!_isSyncingGroupingType && v)
			CurrentGroupingType = GroupingType.IsLocked;
		return default;
	}

	[PropertyChanged(nameof(IsHiddenGroupingSelected))]
	[SuppressMessage("", "IDE0051")]
	ValueTask IsHiddenGroupingSelectedChangedAsync(
		bool v
	)
	{
		if (!_isSyncingGroupingType && v)
			CurrentGroupingType = GroupingType.IsHidden;
		return default;
	}



	[PropertyChanged(nameof(CurrentFilterType))]
	[SuppressMessage("", "IDE0051")]
	ValueTask CurrentFilterTypeChangedAsync(FilterType value)
	{
		if (_isSyncingFilterType)
			return default;

		_isSyncingFilterType = true;
		try
		{
			// 個別boolプロパティを確実に同期
			IsAllFilterSelected = value == FilterType.All;
			IsUnderSeekBarFilterSelected = value == FilterType.UnderSeekBar;
			IsRangeFilterSelected = value == FilterType.Range;

			if (IsRangeFilterSelected)
				EnsureRangeFilterDefaults();

			UpdateFilterHighlight();   // トグルボタンのアイコン判定更新
			FilterItems();             // 絞り込み再実行
		}
		finally
		{
			_isSyncingFilterType = false;
		}

		return default;
	}

	[PropertyChanged(nameof(CurrentGroupingType))]
	[SuppressMessage("", "IDE0051")]
	ValueTask CurrentGroupingTypeChangedAsync(
		GroupingType value
	)
	{
		if (_isSyncingGroupingType)
		{
			return default;
		}

		_isSyncingGroupingType = true;
		try
		{
			// 個別boolプロパティを確実に同期
			IsNoneGroupingSelected =
				value == GroupingType.None;
			IsCategoryGroupingSelected =
				value == GroupingType.Category;
			IsLayerGroupingSelected =
				value == GroupingType.Layer;
			IsGroupGroupingSelected =
				value == GroupingType.Group;
			IsLockedGroupingSelected =
				value == GroupingType.IsLocked;
			IsHiddenGroupingSelected =
				value == GroupingType.IsHidden;

			// ハイライト状態を更新
			UpdateFilterHighlight();
		}
		finally
		{
			_isSyncingGroupingType = false;
		}

		return default;
	}

	[PropertyChanged(nameof(IsCategoryFilterEnabled))]
	[SuppressMessage("", "IDE0051")]
	ValueTask IsCategoryFilterEnabledChangedAsync(bool _)
	{
		UpdateFilterHighlight();
		return default;
	}

}
