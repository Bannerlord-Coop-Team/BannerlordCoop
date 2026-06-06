using Autofac;
using Common;
using Common.Network;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Fields;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Moq;
using System.Collections.Generic;
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

        var builder = new FieldTranspilerCreator(moduleBuilder, typeof(SwitchTestClass), typeId, new FieldInfo[]
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
        var mockNetwork = container.Resolve<Mock<INetwork>>();
        var mockObjectManager = container.Resolve<Mock<IObjectManager>>();

        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        const int classId = 0;

        var syncedFields = new FieldInfo[]
        {
            AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.MyInt)),
            AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.RefClass)),
        };

        var builder = new FieldTranspilerCreator(moduleBuilder, typeof(SwitchTestClass), classId, syncedFields, new Dictionary<FieldInfo, MethodInfo>());

        var transpilerType = builder.Build();

        var harmony = new Harmony("testing");

        var patchMethods = AccessTools.GetDeclaredMethods(typeof(SwitchTestClass));

        foreach (var method in patchMethods)
        {
            harmony.Patch(method, transpiler: new HarmonyMethod(transpilerType.Method("Transpiler")));
        }

        

        var testClass = new SwitchTestClass();

        string instanceId = "MyObj";
        int newValue = 1001;

        mockObjectManager
            .Setup(x => x.TryGetObject<SwitchTestClass>(instanceId, out testClass))
            .Returns(true);

        mockObjectManager
            .Setup(x => x.TryGetId(testClass, out instanceId))
            .Returns(true);

        using (ContainerProvider.UseContainerThreadSafe(container))
        {
            ModInformation.IsServer = true;
            testClass.SetMyInt(newValue);
            testClass.SetMyInt(newValue);

            Assert.Equal(newValue, testClass.MyInt);
        }

        mockNetwork.Verify(x => x.SendAll(
            It.Is<FieldAutoSyncPacket>(packet =>
                packet.classId == classId &&
                packet.fieldId == 0 &&
                packet.instanceId == instanceId &&
                RawSerializer.Deserialize<int>(packet.value) == newValue
            )),
            Times.Once);



        foreach (var method in patchMethods)
        {
            harmony.Unpatch(method, transpilerType.Method("Transpiler"));
        }
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
