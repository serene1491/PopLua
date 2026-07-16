namespace PopLua.Interop;

internal interface IPushable<T>
{
    static abstract void Push(StateStack stack, T value);
}
