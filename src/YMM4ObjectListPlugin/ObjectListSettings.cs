using ObjectList.View;
using YukkuriMovieMaker.Plugin;

namespace ObjectList;

public class ObjectListSettings
	: SettingsBase<ObjectListSettings>
{
	public override SettingsCategory Category =>
		SettingsCategory.None;
	public override string Name => "YMMオブジェクトリスト";

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
	public bool IsShowFooter
	{
		get { return isShowFooter; }
		set { Set(ref isShowFooter, value); }
	}

	public override void Initialize() { }
}
