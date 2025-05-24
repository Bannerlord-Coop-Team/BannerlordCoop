using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders;

public class DynamicSyncPatchBuilder
{
    private readonly IObjectManager objectManager;
    private readonly IDynamicSyncMemberBuilder dynamicSyncMemberBuilder;

    public DynamicSyncPatchBuilder(IObjectManager objectManager,
        IDynamicSyncMemberBuilder dynamicSyncMemberBuilder)
    {
        this.objectManager = objectManager;
        this.dynamicSyncMemberBuilder = dynamicSyncMemberBuilder;
    }

    public List<SyntaxTree> Build(Type declaringType, DynamicSyncRegistryItem dynamicRegistryItem)
    {
        List<string> prefixes = new List<string>();
        List<string> transpilers = new List<string>();
        List<string> messages = new List<string>();
        List<string> messageHandlers = new List<string>();
        List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

        var usings = new HashSet<string>
        {
            declaringType.Namespace
        };

        foreach(var propertyInfo in dynamicRegistryItem.Properties)
        {
            ValidateType(propertyInfo.PropertyType);
            usings.Add(propertyInfo.PropertyType.Namespace);
            prefixes.Add(dynamicSyncMemberBuilder.BuildPrefix(propertyInfo));
            var internalMessage = dynamicSyncMemberBuilder.BuildInternalMessage(propertyInfo);
            var networkMessage = dynamicSyncMemberBuilder.BuildNetworkMessage(propertyInfo);
            var handler = dynamicSyncMemberBuilder.BuildHandler(propertyInfo);

            syntaxTrees.Add(CSharpSyntaxTree.ParseText(internalMessage));
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(networkMessage));
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(handler));


        }

        foreach (var fieldInfo in dynamicRegistryItem.Fields)
        {
            ValidateType(fieldInfo.FieldType);
            usings.Add(fieldInfo.FieldType.Namespace);

            //transpilers.Add(dynamicSyncMemberBuilder.BuildTranspiler(fieldInfo));
            var internalMessage = dynamicSyncMemberBuilder.BuildInternalMessage(fieldInfo);
            var networkMessage = dynamicSyncMemberBuilder.BuildNetworkMessage(fieldInfo);
            var handler = dynamicSyncMemberBuilder.BuildHandler(fieldInfo);

            syntaxTrees.Add(CSharpSyntaxTree.ParseText(internalMessage));
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(networkMessage));
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(handler));
        }

        var patchTemplate = TemplateParser.Parse("Patches.TranspilerTemplate.txt", new
        {
            Libraries = usings,
            TranspilerName = $"{declaringType.Name}_FieldInterceptPatches",
            TargetMethods = dynamicRegistryItem.TargetMethods,
            DeclaringType = declaringType.Name,
            Fields = dynamicRegistryItem.Fields,
        });

        DynamicSyncConfiguration.ExportFile($"{declaringType.Name}/Patches/{declaringType.Name}_DynamicPatch.cs", patchTemplate);

        syntaxTrees.Add(CSharpSyntaxTree.ParseText(patchTemplate));
        

        return syntaxTrees;
    }


    private void ValidateType(Type type)
    {
        Type typeToVerify;
        // TODO:Check if this is enough or if it needs to be restricted more to List,MBList,Queue
        if (type.IsGenericType)
            typeToVerify = type.GetGenericArguments()[0];
        else if (type.IsArray)
            typeToVerify = type.GetElementType();
        else
            typeToVerify = type;
        // Prevent unsupported types
        if (!objectManager.IsTypeManaged(typeToVerify) && !RuntimeTypeModel.Default.CanSerialize(typeToVerify))
        {
            throw new NotSupportedException(
                $"{typeToVerify.Name} is not serializable and not managed by the object manager. " +
                $"Either manage the type using the object manager or make this type serializable");
        }
    }
}
