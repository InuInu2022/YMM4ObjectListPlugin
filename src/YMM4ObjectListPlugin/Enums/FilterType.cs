using System.Reflection;

namespace ObjectList.Enums;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public enum FilterType
{
	All = 0,
	UnderSeekBar = 1,
	Range = 2,
}
