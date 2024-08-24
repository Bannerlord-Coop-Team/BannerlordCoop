using GameInterface.AutoSync.Builders;
using HarmonyLib;
using System;
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
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        const int typeId = 0;

        var builder = new FieldTranspilerCreator(moduleBuilder, typeof(SwitchTestClass), typeId, new FieldInfo[]
        {
            AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.MyInt)),
            AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.RefClass)),
        });

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
}
