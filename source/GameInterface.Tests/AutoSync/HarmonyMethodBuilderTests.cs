using Autofac;
using Common.Extensions;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Server.Policies;
using GameInterface;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.AutoSync.Dynamic;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace GameInterface.Tests.AutoSync;
public class HarmonyMethodBuilderTests
{
    private int TestInt { get; set; } = 5;
    public string StringId = "TestStringId";

    [Fact]
    public void CreateHarmonyMethod()
    {
        var containerBuilder = new ContainerBuilder();

        containerBuilder.RegisterType<DummyPolicy>().As<ISyncPolicy>();

        var container = containerBuilder.Build();

        ContainerProvider.SetContainer(container);

        ModInformation.IsServer = true;

        // Arrange
        var assemblyName = new AssemblyName("AutoSyncDynamicAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var testIntProperty = AccessTools.Property(typeof(HarmonyMethodBuilderTests), nameof(TestInt));
        var eventClassCreator = new EventMessageGenerator();

        var dataClassType = new DataClassGenerator(moduleBuilder).GenerateClass(testIntProperty.PropertyType, "TestObjectData");
        var eventType = eventClassCreator.GenerateEvent(moduleBuilder, testIntProperty);

        var methodGenerator = new HarmonyPatchGenerator(moduleBuilder, testIntProperty, dataClassType, eventType);

        Assert.NotNull(AccessTools.Field(GetType(), "StringId"));


        // Act
        var patchMethod = methodGenerator.GenerateSetterPrefixPatch<HarmonyMethodBuilderTests>(
            testIntProperty.GetSetMethod(true), IdGetterMethod);


        Harmony harmony = new Harmony("This is a test");

        harmony.Patch(testIntProperty.GetSetMethod(true), prefix: new HarmonyMethod(patchMethod));

        // Assert
        TestInt += 1;
    }

    public static string IdGetterMethod(HarmonyMethodBuilderTests instance)
    {
        return instance.StringId;
    }

    [Fact]
    public void SandboxTranspiler()
    {
        //var harmony = new Harmony("asdfasdfasdf");

        //var targetMethod = AccessTools.Constructor(typeof(HandlerExample), new Type[] { 
        //    typeof(IMessageBroker),
        //    typeof(INetwork),
        //    typeof(IObjectManager),
        //    typeof(ILogger), });
        //var transpilerMethod = new HarmonyMethod(GetType(), nameof(Transpiler));
        
        //harmony.Patch(targetMethod, transpiler: transpilerMethod);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var instrs = instructions.ToList();

        return instrs;
    }

    class DummyPolicy : ISyncPolicy
    {
        /// When true this skips patch functionality <see cref="IPolicyProvider"/>
        public bool AllowOriginal() => false;
    }
}
