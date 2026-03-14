using Autofac;
using GameInterface.DynamicSync;
using GameInterface.DynamicSync.Builders;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.DynamicSync.Utils;
using HarmonyLib;
using Moq;
using Xunit;

namespace GameInterface.Tests.DynamicSync;

public class BuildTests
{
    readonly Mock<IObjectManager> objectManagerMock = new Mock<IObjectManager>();

    readonly IContainer container;
    public BuildTests()
    {
        container = DynamicSyncTestContainerBuilder.Build(objectManagerMock);
    }

    [Fact]
    public void BuildFromValueField()
    {
        var dynamicSyncRegistry = container.Resolve<DynamicSyncRegistry>();

        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(FieldTestClass), "MyField"));

        var builder = container.Resolve<DynamicSyncBuilder>();

        builder.Build();
    }

    [Fact]
    public void BuildFromRefField()
    {
        var dynamicSyncRegistry = container.Resolve<DynamicSyncRegistry>();

        objectManagerMock.Setup(x => x.IsTypeManaged(typeof(RefClass))).Returns(true);

        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(FieldRefTestClass), "MyRefField"));

        var builder = container.Resolve<DynamicSyncBuilder>();

        builder.Build();
    }

    [Fact]
    public void BuildFromValueProperty()
    {
        var dynamicSyncRegistry = container.Resolve<DynamicSyncRegistry>();

        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(PropertyTestClass), nameof(PropertyTestClass.MyProperty)));

        var builder = container.Resolve<DynamicSyncBuilder>();

        builder.Build();
    }

    [Fact]
    public void BuildFromRefProperty()
    {
        var dynamicSyncRegistry = container.Resolve<DynamicSyncRegistry>();

        objectManagerMock.Setup(x => x.IsTypeManaged(typeof(RefClass))).Returns(true);

        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(PropertyRefTestClass), nameof(PropertyRefTestClass.MyRefProperty)));

        var builder = container.Resolve<DynamicSyncBuilder>();

        builder.Build();
    }
}
