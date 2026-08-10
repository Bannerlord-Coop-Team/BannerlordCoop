using GameInterface.AutoSync.Templates;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.AutoSync;
public class TemplateRenderTests
{
    private readonly ITestOutputHelper output;

    public TemplateRenderTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void Dynamic_patch_uses_narrowed_automatic_targets_and_preserves_explicit_targets()
    {
        var explicitTarget = typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)!;
        var result = RenderDynamicPatch(true, new[] { explicitTarget });

        Assert.Contains(
            "GenericPatches<TestType_DynamicPatches, TestType>.TranspilerTargets(true)",
            result);
        Assert.Contains("[HarmonyPrepare]", result);
        Assert.Contains("private static bool Prepare() => TargetMethods().Any();", result);
        Assert.Contains("yield return AccessTools.Method(typeof(System.String), \"Trim\");", result);
    }

    [Fact]
    public void Categorized_dynamic_patch_uses_only_its_explicit_targets()
    {
        var explicitTarget = typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)!;
        var result = RenderDynamicPatch(false, new[] { explicitTarget });

        Assert.DoesNotContain("TranspilerTargets(true)", result);
        Assert.Contains("yield return AccessTools.Method(typeof(System.String), \"Trim\");", result);
        Assert.Contains("[HarmonyPrepare]", result);
    }

    [Fact(Skip = "Need regeneration")]
    public void PropertySetPrefixTest()
    {
        var result = TemplateParser.Parse("Patches.PropertySetPrefixTemplate",
            new 
            {
                MemberDeclaringType = "TestType",
                MemberName = "TestProperty",
                MemberType = "int"
            });
        SnapshotAssert.Equals(result);
    }


    [Fact(Skip = "Need regeneration")]
    public void AssemblyInfoTest()
    {
        var result = TemplateParser.Parse("DynamicAssemblyInfoTemplate", new
        {
            Assemblies = new List<string>
            {
                "Assembly1",
                "Assembly2"
            }
        });
        SnapshotAssert.Equals(result);
    }

    [Fact(Skip = "Need regeneration")]
    public void FieldSetTranspilerTest()
    {
        var result = TemplateParser.Parse("Patches.FieldSetTranspilerTemplate", new
        {
            MemberName = "TestField",
            MemberType = "int",
            MessageType = "TestFieldSet"
        });
        SnapshotAssert.Equals(result);
    }

    [Fact(Skip = "Need regeneration")]
    public void FieldListChangeTranspilerTest()
    {
        var result = TemplateParser.Parse("Patches.FieldListChangeTranspilerTemplate", new
        {
            MemberName = "TestFieldList",
            MemberType = "float",
            AddMessageType = "AddListFieldMessage",
            RemoveMessageType = "RemoveListFieldMessage"
        });
        SnapshotAssert.Equals(result);
    }

    [Fact(Skip = "Need regeneration")]
    public void PropertyListChangeTranspilerTest()
    {
        var result = TemplateParser.Parse("Patches.PropertyListChangeTranspilerTemplate", new
        {
            MemberName = "TestPropertyList",
            MemberType = "float",
            AddMessageType = "AddListPropertyMessage",
            RemoveMessageType = "RemoveListPropertyMessage"
        });
        SnapshotAssert.Equals(result);
    }

    [Fact(Skip = "Need regeneration")]
    public void FieldMBListChangeTranspilerTest()
    {
        var result = TemplateParser.Parse("Patches.FieldListChangeTranspilerTemplate", new
        {
            MemberName = "TestFieldMBList",
            MemberType = "double",
            AddMessageType = "AddMBListFieldMessage",
            RemoveMessageType = "RemoveMBListFieldMessage"
        });
        SnapshotAssert.Equals(result);
    }

    [Fact(Skip = "Need regeneration")]
    public void PropertyMBListChangeTranspilerTest()
    {
        var result = TemplateParser.Parse("Patches.PropertyListChangeTranspilerTemplate", new
        {
            MemberName = "TestPropertyMBList",
            MemberType = "double",
            AddMessageType = "AddMBListPropertyMessage",
            RemoveMessageType = "RemoveMBListPropertyMessage"
        });
        SnapshotAssert.Equals(result);
    }

    [Fact(Skip = "Need regeneration")]
    public void FieldQueueChangeTranspilerTest()
    {
        var result = TemplateParser.Parse("Patches.FieldListChangeTranspilerTemplate", new
        {
            MemberName = "TestFieldQueue",
            MemberType = "long",
            AddMessageType = "AddQueueFieldMessage",
            RemoveMessageType = "RemoveQueueFieldMessage"
        });
        SnapshotAssert.Equals(result);
    }

    [Fact(Skip = "Need regeneration")]
    public void PropertyQueueChangeTranspilerTest()
    {
        var result = TemplateParser.Parse("Patches.PropertyListChangeTranspilerTemplate", new
        {
            MemberName = "TestPropertyQueue",
            MemberType = "long",
            AddMessageType = "AddQueuePropertyMessage",
            RemoveMessageType = "RemoveQueuePropertyMessage"
        });
        SnapshotAssert.Equals(result);
    }

    [Fact(Skip = "Need regeneration")]
    public void FieldArrayChangeTranspilerTest()
    {
        var result = TemplateParser.Parse("Patches.FieldListChangeTranspilerTemplate", new
        {
            MemberName = "TestFieldArray",
            MemberType = "string",
            ChangeMessageType = "ChangeArrayFieldMessage"
        });
        SnapshotAssert.Equals(result);
    }

    [Fact(Skip = "Need regeneration")]
    public void PropertyArrayChangeTranspilerTest()
    {
        var result = TemplateParser.Parse("Patches.PropertyListChangeTranspilerTemplate", new
        {
            MemberName = "TestPropertyArray",
            MemberType = "string",
            ChangeMessageType = "ChangeArrayFieldMessage"
        });
        SnapshotAssert.Equals(result);
    }

    private static string RenderDynamicPatch(bool includeDeclaredMethods, MethodInfo[] targetMethods)
    {
        return TemplateParser.Parse("Patches.DynamicPatchTemplate", new
        {
            Libraries = Array.Empty<string>(),
            DeclaringType = "TestType",
            PatchClassName = "TestType_DynamicPatches",
            PatchCategory = includeDeclaredMethods ? null : "TestCategory",
            IncludeDeclaredMethods = includeDeclaredMethods,
            TargetMethods = targetMethods,
            Prefixes = Array.Empty<string>(),
            Transpilers = new[]
            {
                "[HarmonyTranspiler] private static IEnumerable<CodeInstruction> Test(IEnumerable<CodeInstruction> instructions) => instructions;"
            }
        });
    }

}
