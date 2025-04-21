using GameInterface.DynamicSync;
using GameInterface.DynamicSync.Templates;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.IO;
using System;
using System.Reflection;
using System.Linq;
using GameInterface.Services.ObjectManager;
using TaleWorlds.Library;

namespace GameInterface.DynamicSync.Builders;

public class DynamicSyncBuilder
{
    private readonly DynamicSyncRegistry dynamicSyncRegistry;
    private readonly DynamicSyncAssemblyInfoBuilder dynamicSyncAssemblyInfoBuilder;
    private readonly DynamicSyncPatchBuilder dynamicSyncPatchBuilder;
    private readonly IObjectManager objectManager;

    public DynamicSyncBuilder(DynamicSyncRegistry dynamicSyncRegistry, DynamicSyncAssemblyInfoBuilder dynamicSyncAssemblyInfoBuilder, DynamicSyncPatchBuilder dynamicSyncPatchBuilder, IObjectManager objectManager)
    {
        this.dynamicSyncRegistry = dynamicSyncRegistry;
        this.dynamicSyncAssemblyInfoBuilder = dynamicSyncAssemblyInfoBuilder;
        this.dynamicSyncPatchBuilder = dynamicSyncPatchBuilder;
        this.objectManager = objectManager;
    }

    public Assembly Build()
    {
        if (Directory.Exists($@"{DynamicSyncConfiguration.ExportPath}"))
            Directory.Delete($@"{DynamicSyncConfiguration.ExportPath}", true);

        List<Assembly> assemblies = new List<Assembly>
            {
                Assembly.GetExecutingAssembly(),
            };

        // We need to load different dlls based on the runtime
        // currently the games runs .netframework 4.7.2
        // but tests uses .net 6.0
        if (Environment.Version.Major <= 4)
        {
            assemblies.Add(typeof(System.Collections.ArrayList).Assembly);
            assemblies.Add(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly);
            assemblies.Add(typeof(Enumerable).Assembly);
            assemblies.Add(typeof(Queue<>).Assembly);
            assemblies.Add(typeof(Console).Assembly);
        }
        else
        {
            assemblies.Add(typeof(Enumerable).Assembly);
            assemblies.Add(typeof(Queue<>).Assembly);
            assemblies.Add(Assembly.GetExecutingAssembly());
            assemblies.Add(Assembly.Load("System.Runtime"));
            assemblies.Add(Assembly.Load("System.Private.CoreLib"));
            assemblies.Add(Assembly.Load("System.Collections"));
            assemblies.Add(typeof(Console).Assembly);

        }

        foreach (var asm in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
        {
            assemblies.Add(Assembly.Load(asm.FullName));
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if(!asm.IsDynamic && !asm.FullName.Contains("Anonymously Hosted DynamicMethods Assembly") && asm.FullName.Contains("AutoSyncAsm"))
                assemblies.Add(Assembly.Load(asm.FullName));
        }

        List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

        foreach (var registration in dynamicSyncRegistry.Registrations)
        {
            syntaxTrees.AddRange(dynamicSyncPatchBuilder.Build(registration.Key, registration.Value));
        }

        syntaxTrees.Add(dynamicSyncAssemblyInfoBuilder.Build());

        // https://www.strathweb.com/2018/10/no-internalvisibleto-no-problem-bypassing-c-visibility-rules-with-roslyn/
        // Allow IgnoresAccessChecksTo for dynamic compilation
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).
                                    WithMetadataImportOptions(MetadataImportOptions.All);
        var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
        topLevelBinderFlagsProperty.SetValue(compilationOptions, (uint)1 << 22);
        var dynamicAssembly = CSharpCompilation.Create("DynamicSync.dll",
                                                        syntaxTrees: syntaxTrees,
                                                        references:
                                                        assemblies.Select(a => a.Location).Distinct().Select(a => MetadataReference.CreateFromFile(a)),
                                                        options: compilationOptions);
        Assembly assembly;
        using (var assemblyStream = new MemoryStream())
        using (var pdbStream = new MemoryStream())
        {
            var result = dynamicAssembly.Emit(assemblyStream, pdbStream);

            if (!result.Success)
                throw new InvalidOperationException();
            else
                assembly = Assembly.Load(assemblyStream.GetBuffer());
        }

        return assembly;
    }

    //private DynamicPatchInfo GetPatchInfo(Type type, DynamicSyncRegistryItem registryItem, IObjectManager objectManager)
    //{
    //    var dynamicPatchInfo = new DynamicPatchInfo
    //    {
    //        DeclaringType = type,
    //        TargetMethods = registryItem.TargetMethods
    //    };

    //    foreach (var member in registryItem.Members)
    //    {
    //        dynamicPatchInfo.MemberInfos.Add(GetDynamicPatchMemberInfo(member, objectManager));
    //    }

    //    List<string> transpilers = new List<string>();
    //    HashSet<string> usings = new HashSet<string>
    //        {
    //            type.Namespace
    //        };

    //    return dynamicPatchInfo;
    //}

    private DynamicPatchMemberInfo GetDynamicPatchMemberInfo(MemberInfo member, IObjectManager objectManager)
    {
        var patchMemberInfo = new DynamicPatchMemberInfo
        {
            MemberInfo = member,
        };

        Type memberType;
        bool isField = false;
        if (member is FieldInfo fieldInfo)
        {
            memberType = fieldInfo.FieldType;
            isField = true;
            patchMemberInfo.UsingDeclarations.Add(fieldInfo.FieldType.Namespace);
        }
        else
        {
            var propertyInfo = (PropertyInfo)member;
            memberType = propertyInfo.PropertyType;
            patchMemberInfo.UsingDeclarations.Add(propertyInfo.PropertyType.Namespace);
        }

        // Is collection
        bool isObjectMangerType = false;
        var messageUsings = patchMemberInfo.UsingDeclarations.ToList();
        messageUsings.Add(member.DeclaringType.Namespace);
        if (memberType.IsGenericType)
        {
            var genericType = memberType.GenericTypeArguments[0];
            messageUsings.Add(genericType.Namespace);
            patchMemberInfo.UsingDeclarations.Add(genericType.Namespace);
            isObjectMangerType = objectManager.IsTypeManaged(genericType);
            if (typeof(MBList<>).IsAssignableFrom(memberType.GetGenericTypeDefinition()))
            {
                DynamicMessageType messageType = isObjectMangerType ? DynamicMessageType.ObjectManagerType : DynamicMessageType.ValueType;
                messageType |= isField ? DynamicMessageType.Field : DynamicMessageType.Property;

                messageType |= DynamicMessageType.MBList;

                var setMessage = new DynamicMessageInfo
                {
                    Action = DynamicMessageAction.Set,
                    Type = messageType,
                    MessageName = $"{member.DeclaringType.Name}_{member.Name}_SetMessage",
                    UsingDeclarations = messageUsings,
                    ClassType = member.DeclaringType,
                    MemberType = memberType,
                    MemberName = member.Name
                };

                var addMessage = new DynamicMessageInfo
                {
                    Action = DynamicMessageAction.CollectionAdd,
                    Type = messageType,
                    MessageName = $"{member.DeclaringType.Name}_{member.Name}_AddMessage",
                    UsingDeclarations = messageUsings,
                    ClassType = member.DeclaringType,
                    MemberType = genericType,
                    MemberName = member.Name
                };

                var removeMessage = new DynamicMessageInfo
                {
                    Action = DynamicMessageAction.CollectionRemove,
                    Type = messageType,
                    MessageName = $"{member.DeclaringType.Name}_{member.Name}_RemoveMessage",
                    UsingDeclarations = messageUsings,
                    ClassType = member.DeclaringType,
                    MemberType = genericType,
                    MemberName = member.Name
                };
                patchMemberInfo.MessageInfos.Add(setMessage);
                patchMemberInfo.MessageInfos.Add(addMessage);
                patchMemberInfo.MessageInfos.Add(removeMessage);
                patchMemberInfo.PatchType = isField ? DynamicMemberPatchType.FieldMBList : DynamicMemberPatchType.PropertyMBList;
            }
            else if (typeof(List<>).IsAssignableFrom(memberType.GetGenericTypeDefinition()))
            {
                DynamicMessageType messageType = isObjectMangerType ? DynamicMessageType.ObjectManagerType : DynamicMessageType.ValueType;
                messageType |= isField ? DynamicMessageType.Field : DynamicMessageType.Property;

                messageType |= DynamicMessageType.List;

                var setMessage = new DynamicMessageInfo
                {
                    Action = DynamicMessageAction.Set,
                    Type = messageType,
                    MessageName = $"{member.DeclaringType.Name}_{member.Name}_SetMessage",
                    UsingDeclarations = messageUsings,
                    ClassType = member.DeclaringType,
                    MemberType = memberType,
                    MemberName = member.Name
                };

                var addMessage = new DynamicMessageInfo
                {
                    Action = DynamicMessageAction.CollectionAdd,
                    Type = messageType,
                    MessageName = $"{member.DeclaringType.Name}_{member.Name}_AddMessage",
                    UsingDeclarations = messageUsings,
                    ClassType = member.DeclaringType,
                    MemberType = genericType,
                    MemberName = member.Name
                };

                var removeMessage = new DynamicMessageInfo
                {
                    Action = DynamicMessageAction.CollectionRemove,
                    Type = messageType,
                    MessageName = $"{member.DeclaringType.Name}_{member.Name}_RemoveMessage",
                    UsingDeclarations = messageUsings,
                    ClassType = member.DeclaringType,
                    MemberType = genericType,
                    MemberName = member.Name
                };
                patchMemberInfo.MessageInfos.Add(setMessage);
                patchMemberInfo.MessageInfos.Add(addMessage);
                patchMemberInfo.MessageInfos.Add(removeMessage);
                patchMemberInfo.PatchType = isField ? DynamicMemberPatchType.FieldList : DynamicMemberPatchType.PropertyList;

            }
            else if (typeof(Queue<>).IsAssignableFrom(memberType.GetGenericTypeDefinition()))
            {
                DynamicMessageType messageType = isObjectMangerType ? DynamicMessageType.ObjectManagerType : DynamicMessageType.ValueType;
                messageType |= isField ? DynamicMessageType.Field : DynamicMessageType.Property;
                messageType |= DynamicMessageType.Queue;

                var setMessage = new DynamicMessageInfo
                {
                    Action = DynamicMessageAction.Set,
                    Type = messageType,
                    MessageName = $"{member.DeclaringType.Name}_{member.Name}_SetMessage",
                    UsingDeclarations = messageUsings,
                    ClassType = member.DeclaringType,
                    MemberType = memberType,
                    MemberName = member.Name
                };

                var addMessage = new DynamicMessageInfo
                {
                    Action = DynamicMessageAction.CollectionAdd,
                    Type = messageType,
                    MessageName = $"{member.DeclaringType.Name}_{member.Name}_AddMessage",
                    UsingDeclarations = messageUsings,
                    ClassType = member.DeclaringType,
                    MemberType = genericType,
                    MemberName = member.Name
                };

                var removeMessage = new DynamicMessageInfo
                {
                    Action = DynamicMessageAction.CollectionRemove,
                    Type = messageType,
                    MessageName = $"{member.DeclaringType.Name}_{member.Name}_RemoveMessage",
                    UsingDeclarations = messageUsings,
                    ClassType = member.DeclaringType,
                    MemberType = genericType,
                    MemberName = member.Name
                };
                patchMemberInfo.MessageInfos.Add(setMessage);
                patchMemberInfo.MessageInfos.Add(addMessage);
                patchMemberInfo.MessageInfos.Add(removeMessage);
                patchMemberInfo.PatchType = isField ? DynamicMemberPatchType.FieldQueue : DynamicMemberPatchType.PropertyQueue;
            }
        }
        else if (memberType.IsArray)
        {
            isObjectMangerType = objectManager.IsTypeManaged(memberType.GetElementType());
            messageUsings.Add(memberType.GetElementType().Namespace);
            patchMemberInfo.UsingDeclarations.Add(memberType.GetElementType().Namespace);
            DynamicMessageType messageType = isObjectMangerType ? DynamicMessageType.ObjectManagerType : DynamicMessageType.ValueType;
            messageType |= isField ? DynamicMessageType.Field : DynamicMessageType.Property;
            messageType |= DynamicMessageType.Array;

            var setMessage = new DynamicMessageInfo
            {
                Action = DynamicMessageAction.ArraySet,
                Type = messageType,
                MessageName = $"{member.DeclaringType.Name}_{member.Name}_SetMessage",
                UsingDeclarations = messageUsings,
                ClassType = member.DeclaringType,
                MemberType = memberType,
                MemberName = member.Name
            };

            var changeMessage = new DynamicMessageInfo
            {
                Action = DynamicMessageAction.ArrayChange,
                Type = messageType,
                MessageName = $"{member.DeclaringType.Name}_{member.Name}_ChangeMessage",
                UsingDeclarations = messageUsings,
                ClassType = member.DeclaringType,
                MemberType = memberType.GetElementType(),
                MemberName = member.Name
            };
            patchMemberInfo.MessageInfos.Add(setMessage);
            patchMemberInfo.MessageInfos.Add(changeMessage);
            patchMemberInfo.PatchType = isField ? DynamicMemberPatchType.FieldArray : DynamicMemberPatchType.PropertyArray;
        }
        else
        {
            isObjectMangerType = objectManager.IsTypeManaged(memberType);
            DynamicMessageType messageType = isObjectMangerType ? DynamicMessageType.ObjectManagerType : DynamicMessageType.ValueType;
            messageType |= isField ? DynamicMessageType.Field : DynamicMessageType.Property;

            messageType |= DynamicMessageType.Direct;

            var setMessage = new DynamicMessageInfo
            {
                Action = DynamicMessageAction.Set,
                Type = messageType,
                MessageName = $"{member.DeclaringType.Name}_{member.Name}_SetMessage",
                UsingDeclarations = messageUsings,
                ClassType = member.DeclaringType,
                MemberType = memberType,
                MemberName = member.Name
            };
            patchMemberInfo.MessageInfos.Add(setMessage);
            patchMemberInfo.PatchType = isField ? DynamicMemberPatchType.Field : DynamicMemberPatchType.Property;
        }

        return patchMemberInfo;
    }

    public void Dispose()
    {
    }
}
