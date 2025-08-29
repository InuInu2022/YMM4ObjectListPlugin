namespace ObjectList;

public static class AppUtil
{
	public static bool IsDebug { get; } =
#if DEBUG
		true;
#else
		false;
#endif
}
