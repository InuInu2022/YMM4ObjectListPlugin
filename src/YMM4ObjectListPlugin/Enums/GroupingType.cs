using System.ComponentModel.DataAnnotations;
using System.Reflection;

using YukkuriMovieMaker.Resources.Localization;

namespace ObjectList.Enums;

/// <summary>
/// Specifies the available grouping types for objects in the plugin.
/// </summary>
/// <remarks>
/// Each value represents a different way to group objects, such as by category, layer, group, lock state, or visibility.
/// </remarks>
[Obfuscation(Exclude = true, ApplyToMembers = true)]
public enum GroupingType
{
	[Display(
		Name = "なし",
		Description = "グルーピングしません"
	)]
	None = 0,

	[Display(
		Name = "カテゴリ",
		Description = "グルーピングしません"
	)]
	Category = 1,

	[Display(
		Name = nameof(Texts.BaseItemLayerName),
		ResourceType = typeof(Texts)
	)]
	Layer = 2,

	[Display(
		Name = nameof(Texts.GroupLabel),
		ResourceType = typeof(Texts)
	)]
	Group = 3,

	[Display(
		Name = nameof(Texts.BaseItemIsLockedName),
		ResourceType = typeof(Texts)
	)]
	IsLocked = 4,

	[Display(
		Name = nameof(Texts.BaseItemIsHiddenName),
		ResourceType = typeof(Texts)
	)]
	IsHidden = 5,
}
