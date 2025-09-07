using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ObjectList.Enums;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public enum LengthViewMode
{
	[Display(
		Name = "フレーム数",
		Description = "フレーム数を表示します"
	)]
	Frame,

	[Display(
		Name = "秒数",
		Description = "秒数を表示します"
	)]
	Seconds,

	[Display(
		Name = "スマート",
		Description = "1秒未満はフレーム数、1秒以上は秒数表示"
	)]
	Smart,
}
