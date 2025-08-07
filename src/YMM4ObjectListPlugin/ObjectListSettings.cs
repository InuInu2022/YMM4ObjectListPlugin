using System.Reflection;

using ObjectList.ViewModel;

using YukkuriMovieMaker.Plugin;

namespace ObjectList;

public class ObjectListSettings
	: SettingsBase<ObjectListSettings>
{
	public override SettingsCategory Category =>
		SettingsCategory.None;
	public override string Name => "YMM4オブジェクトリスト";

	public override bool HasSettingView => true;
	public override object? SettingView
	{
		get
		{
			return new ObjectList.View.SettingView
			{
				DataContext =
					new ViewModel.SettingViewModel(),
			};
		}
	}

	bool isShowFooter = true;
	private bool isShowColumnColor = true;
	private bool isShowColumnCategory = true;
	private bool isShowColumnFrame;
	private bool isShowColumnLength;
	private bool isShowColumnLayer = true;
	private ColumLayerType showColumLayerType =
		ColumLayerType.Number;
	private bool isShowColumnGroup = true;
	private bool isShowColumnLock = true;
	private bool isShowColumnHidden = true;
	private bool isShowFooterSceneName = true;
	private bool isShowFooterSceneFps = true;
	private bool isShowFooterSceneHz = true;
	private bool isShowFooterSceneScreenSize;

	private GroupingType selectedGroupingType =
		GroupingType.None;

	private FilterType selectedFilterType = FilterType.All;
	private bool rangeFilterStrictMode = true;

	#region footer
	public bool IsShowFooter
	{
		get => isShowFooter;
		set => Set(ref isShowFooter, value);
	}

	public bool IsShowFooterSceneName
	{
		get => isShowFooterSceneName;
		set => Set(ref isShowFooterSceneName, value);
	}

	public bool IsShowFooterSceneFps
	{
		get => isShowFooterSceneFps;
		set => Set(ref isShowFooterSceneFps, value);
	}

	public bool IsShowFooterSceneHz
	{
		get => isShowFooterSceneHz;
		set => Set(ref isShowFooterSceneHz, value);
	}

	public bool IsShowFooterSceneScreenSize
	{
		get => isShowFooterSceneScreenSize;
		set => Set(ref isShowFooterSceneScreenSize, value);
	}

	#endregion footer

	#region column

	public bool IsShowColumnColor
	{
		get => isShowColumnColor;
		set => Set(ref isShowColumnColor, value);
	}

	public bool IsShowColumnCategory
	{
		get => isShowColumnCategory;
		set => Set(ref isShowColumnCategory, value);
	}
	public bool IsShowColumnFrame
	{
		get => isShowColumnFrame;
		set => Set(ref isShowColumnFrame, value);
	}
	public bool IsShowColumnLength
	{
		get => isShowColumnLength;
		set => Set(ref isShowColumnLength, value);
	}
	public bool IsShowColumnLayer
	{
		get => isShowColumnLayer;
		set => Set(ref isShowColumnLayer, value);
	}

	public ColumLayerType ShowColumLayerType
	{
		get => showColumLayerType;
		set => Set(ref showColumLayerType, value);
	}

	public bool IsShowColumnGroup
	{
		get => isShowColumnGroup;
		set => Set(ref isShowColumnGroup, value);
	}

	public bool IsShowColumnLock
	{
		get => isShowColumnLock;
		set => Set(ref isShowColumnLock, value);
	}

	public bool IsShowColumnHidden
	{
		get => isShowColumnHidden;
		set => Set(ref isShowColumnHidden, value);
	}

	#endregion column

	public GroupingType SelectedGroupingType
	{
		get => selectedGroupingType;
		set => Set(ref selectedGroupingType, value);
	}

	public FilterType SelectedFilterType
	{
		get => selectedFilterType;
		set => Set(ref selectedFilterType, value);
	}

	public bool RangeFilterStrictMode
	{
		get => rangeFilterStrictMode;
		set => Set(ref rangeFilterStrictMode, value);
	}

	public override void Initialize() { }
}

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public enum ColumLayerType
{
	Number = 0,
	Name = 1,

	None = 99,
}

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public enum FilterType
{
	All = 0,
	UnderSeekBar = 1,
	Range = 2,
}
