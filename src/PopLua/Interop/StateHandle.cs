namespace PopLua.Interop;

internal readonly record struct StateHandle(nint Value)
{
    internal bool IsNull => Value == 0;
    internal static StateHandle Null => new(0);
}
