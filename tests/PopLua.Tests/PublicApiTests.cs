using System.ComponentModel;
using System.Reflection;

namespace PopLua.Tests;

public sealed class PublicApiTests
{
    [Fact]
    public void GeneratedBindingSupportTypesAreHiddenFromNormalBrowsing()
    {
        AssertHidden(typeof(Marshaller));
        AssertHidden(typeof(GeneratedModuleRegistry));
        AssertHidden(typeof(GeneratedUserdataRegistry));
        AssertHidden(typeof(Registration));
        AssertHidden(typeof(IGeneratedModule));
    }

    [Fact]
    public void SessionDoesNotExposePublicGlobalMutationApi()
    {
        var publicMethods = typeof(Session)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(m => m.Name)
            .ToArray();

        Assert.DoesNotContain("SetGlobal", publicMethods);
        Assert.DoesNotContain("SetGlobalNil", publicMethods);
    }

    [Fact]
    public void SandboxQuotaUsesExplicitActiveAndWallTimeParameters()
    {
        var quota = Assert.Single(
            typeof(Builder).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            m => m.Name == nameof(Builder.Quota));

        var parameters = quota.GetParameters().Select(p => p.Name).ToArray();

        Assert.Contains("activeTime", parameters);
        Assert.Contains("wallTime", parameters);
        Assert.DoesNotContain("time", parameters);
    }

    private static void AssertHidden(Type type)
    {
        var attribute = type.GetCustomAttribute<EditorBrowsableAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(EditorBrowsableState.Never, attribute.State);
    }
}
