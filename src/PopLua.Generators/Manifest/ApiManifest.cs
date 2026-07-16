using System.Collections.Generic;

namespace PopLua.Generators.Manifest;

internal sealed class ApiManifest
{
    public ApiManifest(string popluaVersion, string assemblyIdentity)
    {
        PopLuaVersion = popluaVersion;
        AssemblyIdentity = assemblyIdentity;
    }

    public ApiManifest(string popluaVersion)
        : this(popluaVersion, string.Empty)
    {
    }

    public int SchemaVersion => ManifestConstants.SchemaVersion;
    public string SchemaId => ManifestConstants.SchemaId;
    public string PopLuaVersion { get; }
    public string AssemblyIdentity { get; }
    public List<ModuleModel> Modules { get; } = [];
    public List<UserdataModel> Userdata { get; } = [];
    public List<DescriptorModel> Descriptors { get; } = [];
}
