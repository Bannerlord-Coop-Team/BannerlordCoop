using Autofac;
using Common.Extensions;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Policies;
using GameInterface;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.AutoSync.Dynamic;
using GameInterface.Utils.AutoSync.Example;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace Coop.Tests.AutoSync;
public class HarmonyMethodBuilderTests
{
    private int TestInt { get; set; } = 5;
    public string StringId = "TestStringId";

    [Fact]
    public void CreateHarmonyMethod()
    {
        var containerBuilder = new ContainerBuilder();

        containerBuilder.RegisterType<ServerSyncPolicy>().As<ISyncPolicy>();

        var container = containerBuilder.Build();

        ContainerProvider.SetContainer(container);

        var methods = typeof(ILogger).GetMethods().Where(m => m.Name.Contains("Error")).ToArray();

        var m = methods[2];

        // Arrange
        var assemblyName = new AssemblyName("AutoSyncDynamicAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");


        var typeBuilder = moduleBuilder.DefineType(
            "TestClass",
            TypeAttributes.Public |
            TypeAttributes.Class |
            TypeAttributes.AnsiClass |
            TypeAttributes.AutoLayout |
            TypeAttributes.BeforeFieldInit |
            TypeAttributes.AutoLayout,
            null);

        ConstructorBuilder constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        ILGenerator ilGenerator = constructor.GetILGenerator();
        ilGenerator.Emit(OpCodes.Ldarg_0);
        ilGenerator.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
        ilGenerator.Emit(OpCodes.Ret);

        var methodGenerator = new HarmonyPatchGenerator(moduleBuilder, typeBuilder);

        Assert.NotNull(AccessTools.Field(GetType(), "StringId"));


        // Act
        var testIntProperty = AccessTools.Property(typeof(HarmonyMethodBuilderTests), nameof(TestInt));
        var patchMethod = methodGenerator.GenerateSetterPrefixPatch<HarmonyMethodBuilderTests>(
            testIntProperty.GetSetMethod(true), IdGetterMethod);

        var type = typeBuilder.CreateType();

        var method = AccessTools.Method(type, patchMethod.Name).BuildDelegate<Func<HarmonyMethodBuilderTests, int, bool>>();

        // Assert
        ModInformation.IsServer = true;
        Assert.True(method(this, 3));
    }

    public static string IdGetterMethod(HarmonyMethodBuilderTests getter)
    {
        return getter.StringId;
    }

    [Fact]
    public void SandboxTranspiler()
    {
        var harmony = new Harmony("asdfasdfasdf");

        var targetMethod = AccessTools.Constructor(typeof(HandlerExample), new Type[] { 
            typeof(IMessageBroker),
            typeof(INetwork),
            typeof(IObjectManager),
            typeof(ILogger), });
        var transpilerMethod = new HarmonyMethod(GetType(), nameof(Transpiler));
        
        harmony.Patch(targetMethod, transpiler: transpilerMethod);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var instrs = instructions.ToList();

        return instrs;
    }
}
