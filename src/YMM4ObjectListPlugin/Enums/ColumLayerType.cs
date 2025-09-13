using System.Reflection;

namespace ObjectList.Enums;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public enum ColumLayerType
{
	Number = 0,
	Name = 1,

	None = 99,
}
