namespace PopLua.Binding;

internal sealed class UserdataDescriptor
{
    internal UserdataDescriptor(string metatableName, Action<Registration> register)
    {
        MetatableName = metatableName;
        Register = register;
    }

    internal string MetatableName { get; }
    internal Action<Registration> Register { get; }
}
