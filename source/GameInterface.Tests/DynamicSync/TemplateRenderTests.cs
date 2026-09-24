using GameInterface.AutoSync.Templates;
using System.Collections.Generic;
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
    public void InterfaceReferenceSet_UsesAvailableHandleApis()
    {
        var result = TemplateParser.Parse("Handlers.SubscribeSetReferenceTemplate", new
        {
            Interface = true,
            MemberType = "ITestValue",
            MemberDeclaringType = "TestOwner",
            MemberDeclaringTypeName = "TestOwner",
            MemberName = "Value",
            ReadOnly = false,
            Debug = false,
        });

        Assert.Contains("TryGetHandle(data.Value, out valueId)", result);
        Assert.Contains("TryGetObjectWithLogging(data.ValueId, out value)", result);
        Assert.DoesNotContain("TryGetHandle(data.Value.GetType(), data.Value", result);
        Assert.DoesNotContain("TryGetObjectWithLogging(type, data.ValueId", result);
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

}
