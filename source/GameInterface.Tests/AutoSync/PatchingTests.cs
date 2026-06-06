using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.AutoSync.Fields;
using GameInterface.AutoSync.Properties;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using LiteNetLib;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using static GameInterface.Tests.AutoSync.AutoSyncTests;

namespace GameInterface.Tests.AutoSync;
public class PatchingTests
{
    [Fact]
    public void PropertyPatchCreation()
    {
        // Arrange
        var container = CreateContainer();

        Assert.NotNull(typeof(ProtoBufSerializer).GetMethods().Where(m => m.Name == nameof(ProtoBufSerializer.Deserialize) && m.IsGenericMethod));

        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        var props = AccessTools.GetDeclaredProperties(typeof(SwitchTestClass)).ToArray();

        var patchCreator = new PropertyPrefixCreator(moduleBuilder, typeof(SwitchTestClass), 0, props);

        // Act
        var patchType = patchCreator.Build();

        // Assert
        Assert.NotEmpty(props);

        foreach (var prop in props)
        {
            Assert.NotNull(AccessTools.Method(patchType, $"{prop.DeclaringType!.Name}_{prop.Name}_Prefix"));
        }
    }

    [Fact]
    public void PropertyPatchSendsPacket()
    {
        // Arrange
        var container = CreateContainer();

        var mockObjectManager = container.Resolve<Mock<IObjectManager>>();
        var mockNetwork = container.Resolve<Mock<INetwork>>();

        ModInformation.IsServer = true;
        Assert.NotNull(typeof(ProtoBufSerializer).GetMethods().Where(m => m.Name == nameof(ProtoBufSerializer.Deserialize) && m.IsGenericMethod));

        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        var props = AccessTools.GetDeclaredProperties(typeof(SwitchTestClass)).ToArray();

        var patchCreator = new PropertyPrefixCreator(moduleBuilder, typeof(SwitchTestClass), 0, props);

        var patchType = patchCreator.Build();

        var prop = props.First();

        
        using (ContainerProvider.UseContainerThreadSafe(container));
        var prefix = AccessTools.Method(patchType, $"{prop.DeclaringType!.Name}_{prop.Name}_Prefix");
        Assert.NotNull(prefix);

        var testInstance = new SwitchTestClass();
        var objId = "ObjId1";

        mockObjectManager
            .Setup(x => x.TryGetObject<SwitchTestClass>(objId, out testInstance))
            .Returns(true);

        mockObjectManager
            .Setup(x => x.TryGetId(testInstance, out objId))
            .Returns(true);

        // Act
        prefix.Invoke(null, new object[] { testInstance, 5 });

        // Assert
        mockNetwork.Verify(x => x.SendAll(It.IsAny<PropertyAutoSyncPacket>()), Times.Once);
    }

    private IContainer CreateContainer()
    {
        var builder = new ContainerBuilder();

        RegisterMock<IObjectManager>(builder);
        RegisterMock<INetwork>(builder);

        return builder.Build();
    }

    private void RegisterMock<T>(ContainerBuilder builder) where T : class
    {
        builder.Register(ctx =>
        {
            var mock = new Mock<T>();
            return mock;
        }).AsSelf().InstancePerLifetimeScope();

        builder.Register(ctx =>
        {
            var mock = ctx.Resolve<Mock<T>>();
            return mock.Object;
        }).As<T>().InstancePerLifetimeScope();
    }
}
