using Autofac;
using Common.Network;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Fields;
using GameInterface.AutoSync.Properties;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using Xunit.Abstractions;
using static GameInterface.Tests.AutoSync.AutoSyncTests;

namespace GameInterface.Tests.AutoSync;
public class TranspilerTests
{
    private readonly ITestOutputHelper output;

    public TranspilerTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void TranspileTest()
    {
        var container = CreateContainer();

        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        const int typeId = 0;

        var builder = new FieldTranspilerCreator(container.Resolve<IObjectManager>(), moduleBuilder, typeof(SwitchTestClass), typeId, new FieldInfo[]
        {
            AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.MyInt)),
            AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.RefClass)),
        }, new Dictionary<FieldInfo, MethodInfo>());

        var transpilerType = builder.Build();

        var harmony = new Harmony("testing");

        var patchMethods = AccessTools.GetDeclaredMethods(typeof(SwitchTestClass));

        foreach (var method in patchMethods)
        {
            harmony.Patch(method, transpiler: new HarmonyMethod(transpilerType.Method("Transpiler")));
        }

        foreach (var method in patchMethods)
        {
            harmony.Unpatch(method, transpilerType.Method("Transpiler"));
        }
    }

    [Fact]
    public void InterceptSendsPacket()
    {
        var container = CreateContainer();
        var network = container.Resolve<TestNet>();
        var objManager = container.Resolve<IObjectManager>();

        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        const int typeId = 0;

        var syncedFields = new FieldInfo[]
        {
            AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.MyInt)),
            AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.RefClass)),
        };

        var builder = new FieldTranspilerCreator(objManager, moduleBuilder, typeof(SwitchTestClass), typeId, syncedFields, new Dictionary<FieldInfo, MethodInfo>());

        var transpilerType = builder.Build();

        var harmony = new Harmony("testing");

        var patchMethods = AccessTools.GetDeclaredMethods(typeof(SwitchTestClass));

        foreach (var method in patchMethods)
        {
            harmony.Patch(method, transpiler: new HarmonyMethod(transpilerType.Method("Transpiler")));
        }

        

        var testClass = new SwitchTestClass();

        const string instanceId = "MyObj";
        const int newValue = 1001;
        objManager.AddExisting(instanceId, testClass);

        
        using (ContainerProvider.UseContainerThreadSafe(container))
        {
            ModInformation.IsServer = true;
            testClass.SetMyInt(newValue);

            Assert.Equal(newValue, testClass.MyInt);
        }

        var packet = Assert.IsType<FieldAutoSyncPacket>(network.SentPackets.First());

        Assert.Equal(typeId, packet.typeId);
        Assert.Equal(0, packet.fieldId);
        Assert.Equal(instanceId, packet.instanceId);
        Assert.Equal(newValue, RawSerializer.Deserialize<int>(packet.value));
        


        foreach (var method in patchMethods)
        {
            harmony.Unpatch(method, transpilerType.Method("Transpiler"));
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
