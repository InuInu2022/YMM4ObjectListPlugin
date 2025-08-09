using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using YmmeUtil.Bridge.Wrap.Items;
using YmmeUtil.Bridge.Wrap.ViewModels;
using YukkuriMovieMaker.Resources.Localization;
using System.Runtime.InteropServices;

namespace ObjectList.ViewModel;

//[ViewModel]
public class ObjectListItem
	: INotifyPropertyChanged,
		IDisposable
{
	private readonly IWrapBaseItem _item;
	readonly WrapTimelineItemViewModel _itemVm;

	static readonly List<ObjectListItem> instances = [];

	public ObjectListItem(
		WrapTimelineItemViewModel itemVm,
		int fps
	)
	{
		_itemVm = itemVm;
		_item = itemVm.Item;
		FPS = fps;
		// アイテムの変更を監視
		if (_item is INotifyPropertyChanged notifyItem)
		{
			notifyItem.PropertyChanged += OnItemPropertyChanged;
		}
		RawItemCategory = _item.RawItem.GetType().Name;
		instances.Add(this);
	}

	public string Label => _item.Label;
	public int Group => _item.Group;
	public int Layer => _item.Layer;
	public int Length => _item.Length;

	public int FPS { get; set; }

	public TimeSpan ContentLength =>
		_item.RawItem.ContentLength;

	static LengthViewMode showLengthViewMode =
		ObjectListSettings.Default.ShowLengthViewMode;
	public static LengthViewMode ShowLengthViewMode
	{
		get => showLengthViewMode;
		set
		{
			if (showLengthViewMode == value)
			{
				return;
			}
			showLengthViewMode = value;
			foreach (
				ref var item in CollectionsMarshal.AsSpan(
					instances
				)
			)
			{
				item.OnPropertyChanged(
					nameof(DisplayLength)
				);
			}
		}
	}
	public string DisplayLength
	{
		get
		{
			return ShowLengthViewMode switch
			{
				LengthViewMode.Frame => $"{Length}",
				LengthViewMode.Seconds =>
					$"{(Length / FPS):0.#}{Texts.Sec}",
				LengthViewMode.Smart =>
					ContentLength.TotalSeconds < 1.0
						? $"{Length}"
						: $"{(Length / FPS):0.#}{Texts.Sec}",
				_ => $"{Length}",
			};
		}
	}

	public int Frame => _item.Frame;


	public Brush ColorBrush => new SolidColorBrush(_item.ItemColor);

	public string Category
	{
		get
		{
			//Debug.WriteLine($"Category: {RawItemCategory}");
			return RawItemCategory switch
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

	public string CategoryIconKey =>
		RawItemCategory switch
		{
			"VideoItem" => "Video",
			"AudioItem" => "Music",
			"ImageItem" => "Image",
			"TextItem" => "FormatText",
			"FrameBufferItem" => "ImageMultiple",
			"EffectItem" => "ImageAutoAdjust",
			"SceneItem" => "ChartTimeline",
			"TransitionItem" => "GradientHorizontal",
			"TachieItem" => "Account",
			"TachieFaceItem" => "EmoticonOutline",
			"ShapeItem" => "ShapePlus",
			"VoiceItem" => "MessageTextOutline",
			"GroupItem" => "SelectGroup",
			_ => "icon_other",
		};

	public string RawItemCategory { get; }

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
				OnPropertyChanged(nameof(DisplayLength));
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

	public void Dispose()
	{
		instances.Remove(this);
		if (_item is INotifyPropertyChanged notifyItem)
		{
			notifyItem.PropertyChanged -=
				OnItemPropertyChanged;
		}
	}
}
