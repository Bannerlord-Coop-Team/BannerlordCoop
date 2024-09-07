using Autofac;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Builders;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using LiteNetLib;
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
        Assert.NotNull(typeof(ProtoBufSerializer).GetMethods().Where(m => m.Name == nameof(ProtoBufSerializer.Deserialize) && m.IsGenericMethod));

        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        var props = AccessTools.GetDeclaredProperties(typeof(SwitchTestClass)).ToArray();

        var patchCreator = new PropertySyncByValuePatchCreator(moduleBuilder, typeof(SwitchTestClass));

        var patchType = patchCreator.Build(0, props);

        Assert.NotEmpty(props);

        foreach (var prop in props)
        {
            Assert.NotNull(AccessTools.Method(patchType, $"{prop.DeclaringType!.Name}_{prop.Name}_Prefix"));
        }
    }

    [Fact]
    public void PropertyPatchSendsPacket()
    {
        ModInformation.IsServer = true;
        Assert.NotNull(typeof(ProtoBufSerializer).GetMethods().Where(m => m.Name == nameof(ProtoBufSerializer.Deserialize) && m.IsGenericMethod));

        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        var props = AccessTools.GetDeclaredProperties(typeof(SwitchTestClass)).ToArray();

        var patchCreator = new PropertySyncByValuePatchCreator(moduleBuilder, typeof(SwitchTestClass));

        var patchType = patchCreator.Build(0, props);

        var container = CreateContainer();

        var network = container.Resolve<TestNet>();
        var objManager = container.Resolve<IObjectManager>();
        var prop = props.First();

        using (ContainerProvider.UseContainerThreadSafe(container))
        {
            var prefix = AccessTools.Method(patchType, $"{prop.DeclaringType!.Name}_{prop.Name}_Prefix");
            Assert.NotNull(prefix);

            var testInstance = new SwitchTestClass();

            var objId = "ObjId1";
            objManager.AddExisting(objId, testInstance);

            prefix.Invoke(null, new object[] { testInstance, 5 });

            var packet = Assert.IsType<AutoSyncFieldPacket>(Assert.Single(network.SentPackets));

            Assert.Equal(objId, packet.instanceId);
        }
    }

    private IContainer CreateContainer()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<TestNet>().AsSelf().As<INetwork>().InstancePerLifetimeScope();
        builder.RegisterType<TestObjManager>().AsSelf().As<IObjectManager>().InstancePerLifetimeScope();

        return builder.Build();
    }
}

public class TestNet : INetwork
{
    public readonly List<IPacket> SentPackets = new List<IPacket>();

    public INetworkConfiguration Configuration => throw new System.NotImplementedException();

    public void Dispose()
    {
    }

    public void Send(NetPeer netPeer, IPacket packet)
    {
        throw new System.NotImplementedException();
    }

    public void Send(NetPeer netPeer, IMessage message)
    {
        throw new System.NotImplementedException();
    }

    public void SendAll(IPacket packet)
    {
        SentPackets.Add(packet);
    }

    public void SendAll(IMessage message)
    {
        throw new System.NotImplementedException();
    }

    public void SendAllBut(NetPeer excludedPeer, IPacket packet)
    {
        throw new System.NotImplementedException();
    }

    public void SendAllBut(NetPeer excludedPeer, IMessage message)
    {
        throw new System.NotImplementedException();
    }

    public void Start()
    {
        throw new System.NotImplementedException();
    }
}
