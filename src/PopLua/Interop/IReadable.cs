namespace PopLua.Interop;

internal interface IReadable<T>
{
    static abstract T Read(StateStack stack, int index);
}
