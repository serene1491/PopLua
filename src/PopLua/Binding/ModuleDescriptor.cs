namespace PopLua.Binding;

internal sealed class ModuleDescriptor
{
    internal ModuleDescriptor(string name, string? cap, Action<Registration> register)
    {
        Name = name;
        Cap = cap;
        Register = register;
    }

    internal string Name { get; }
    internal string? Cap { get; }
    internal Action<Registration> Register { get; }
}
