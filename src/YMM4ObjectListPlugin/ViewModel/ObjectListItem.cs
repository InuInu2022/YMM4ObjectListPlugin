using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using YmmeUtil.Bridge.Wrap.Items;
using YmmeUtil.Bridge.Wrap.ViewModels;
using YukkuriMovieMaker.Resources.Localization;

namespace ObjectList.ViewModel;

//[ViewModel]
public class ObjectListItem : INotifyPropertyChanged
{
	private readonly IWrapBaseItem _item;
	readonly WrapTimelineItemViewModel _itemVm;

	public ObjectListItem(WrapTimelineItemViewModel itemVm)
	{
		_itemVm = itemVm;
		_item = itemVm.Item;
		// アイテムの変更を監視
		if (_item is INotifyPropertyChanged notifyItem)
		{
			notifyItem.PropertyChanged += OnItemPropertyChanged;
		}
	}

	public string Label => _item.Label;
	public int Group => _item.Group;
	public int Layer => _item.Layer;
	public int Length => _item.Length;

	public int Frame => _item.Frame;


	public Brush ColorBrush => new SolidColorBrush(_item.ItemColor);

	public string Category
	{
		get
		{
			Debug.WriteLine($"Category: {_item.RawItem.GetType().Name}");
			return _item.RawItem.GetType().Name switch
			{
				"VideoItem" => Texts.VideoItemName,
				"AudioItem" => Texts.AudioItemName,
				"ImageItem" => Texts.ImageItemName,
				"TextItem" => Texts.TextItemName,
				"FrameBufferItem" => Texts.FrameBufferItemName,
				"EffectItem" => Texts.EffectItemName,
				"SceneItem" => Texts.SceneGroupName,
				"TransitionItem" => Texts.TransitionItemName,
				"TachieItem" => Texts.TachieItemName,
				"TachieFaceItem" => Texts.TachieFaceItemName,
				"ShapeItem" => Texts.ShapeItemName,
				"VoiceItem" => Texts.VoiceItemName,
				"GroupItem" => Texts.GroupItemName,
				_ => Texts.EditorOtherGroupName,
			};
		}
	}

	public bool IsLocked
	{
		get => _item.IsLocked;
		set => _item.IsLocked = value;
	}

	public bool IsHidden
	{
		get => _item.IsHidden;
		set => _item.IsHidden = value;
	}

	public string IsLockedLabel =>
		IsLocked ? "🔒 Lock" : "🔓 Unlock";
	public string IsHiddenLabel => IsHidden ? "🙈" : "👁";

	void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// アイテムのプロパティが変更されたら、対応するプロパティの変更通知を送る
		switch (e.PropertyName)
		{
			case nameof(IWrapBaseItem.Label):
				OnPropertyChanged(nameof(Label));
				break;
			case nameof(IWrapBaseItem.Group):
				OnPropertyChanged(nameof(Group));
				break;
			case nameof(IWrapBaseItem.Layer):
				OnPropertyChanged(nameof(Layer));
				break;
			case nameof(IWrapBaseItem.ItemColor):
				OnPropertyChanged(nameof(ColorBrush));
				break;
			case nameof(IWrapBaseItem.IsLocked):
				OnPropertyChanged(nameof(IsLocked));
				OnPropertyChanged(nameof(IsLockedLabel));
				break;
			case nameof(IWrapBaseItem.IsHidden):
				OnPropertyChanged(nameof(IsHidden));
				OnPropertyChanged(nameof(IsHiddenLabel));
				break;
			case nameof(IWrapBaseItem.Length):
				OnPropertyChanged(nameof(Length));
				break;
			case nameof(IWrapBaseItem.Frame):
				OnPropertyChanged(nameof(Frame));
				break;
			default:
				break;
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public IWrapBaseItem ConvertToWrapItem() => _item;

	public WrapTimelineItemViewModel ConvertToItemViewModel() => _itemVm;
}
