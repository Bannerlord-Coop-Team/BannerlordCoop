using GameInterface.Services.Modules;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Modules;

public sealed class CoopModulePathResolverTests
{
    private const string XmlName = "coop_fixed_town_npcs";

    [Theory]
    [InlineData(CoopModulePathResolver.StableModuleId)]
    [InlineData(CoopModulePathResolver.NightlyModuleId)]
    public void GetXmlPath_UsesActiveModuleVariant(string activeModuleId)
    {
        var resolver = new CoopModulePathResolver(
            moduleId => string.Equals(moduleId, activeModuleId, StringComparison.Ordinal),
            (moduleId, xmlName) => moduleId + "/ModuleData/" + xmlName + ".xml");

        string path = resolver.GetXmlPath(XmlName);

        Assert.Equal(activeModuleId + "/ModuleData/" + XmlName + ".xml", path);
    }

    [Fact]
    public void GetXmlPath_ReturnsNullWithoutActiveCoopVariant()
    {
        var resolver = new CoopModulePathResolver(
            _ => false,
            (_, _) => throw new InvalidOperationException("Path lookup should not run"));

        Assert.Null(resolver.GetXmlPath(XmlName));
    }
}
