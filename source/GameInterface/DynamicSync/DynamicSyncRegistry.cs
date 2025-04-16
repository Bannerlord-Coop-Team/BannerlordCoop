using Common.Messaging;
using GameInterface.DynamicSync.Builders;
using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.Diamond;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;

namespace GameInterface.DynamicSync
{
    public class DynamicSyncRegistry
    {
        private readonly IObjectManager objectManager;
        private readonly DynamicSyncPatchProcessor dynamicSyncPatchProcessor;

        public readonly Dictionary<Type, DynamicSyncRegistryItem> Registrations = new Dictionary<Type, DynamicSyncRegistryItem>();
        
        string DebugPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\DynamicSyncDebug";

        public void AddField(FieldInfo field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));

            // TODO: Add back collection support
            if (field.FieldType.IsGenericType || field.FieldType.IsArray) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Field: Collection types are currently not supported");

            // TODO: verify interface support
            if (field.FieldType.IsInterface) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Field: Interfaces are currently not supported");

            if (!AddMember(field.DeclaringType, field)) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Field: {field.Name} has already been registered as a synced field");
        }

        public void AddProperty(PropertyInfo property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));

            // only prevent properties from being added if they are no collection like type
            if (property.CanWrite == false) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Property: {property.Name} does not have a set method");

            // TODO: Add back collection support
            if (property.PropertyType.IsGenericType || property.PropertyType.IsArray) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Property: Collection types are currently not supported");

            // TODO: verify interface support
            if (property.PropertyType.IsInterface) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Property: Interfaces are currently not supported");

            if (!AddMember(property.DeclaringType, property)) throw new ArgumentException($"{nameof(DynamicSyncBuilder)} Property: {property.Name} has already been registered as a synced property");
        }

        public bool AddTargetMethod(Type type, MethodInfo methodInfo)
        {

            if (!Registrations.ContainsKey(type))
            {
                Registrations.Add(type, new DynamicSyncRegistryItem());
            }

            if (Registrations[type].TargetMethods.Contains(methodInfo))
                return false;

            Registrations[type].TargetMethods.Add(methodInfo);

            return true;
        }

        private bool AddMember(Type type, MemberInfo memberInfo)
        {
            if (memberInfo is not FieldInfo && memberInfo is not PropertyInfo)
                return false;

            if (!Registrations.ContainsKey(type))
            {
                Registrations.Add(type, new DynamicSyncRegistryItem());
            }
            if(memberInfo is FieldInfo fieldInfo)
            { 
                if (Registrations[type].Fields.Contains(fieldInfo))
                    return false;

                Registrations[type].Fields.Add(fieldInfo);
            }
            else if (memberInfo is PropertyInfo propertyInfo)
            {
                if (Registrations[type].Properties.Contains(propertyInfo))
                    return false;

                Registrations[type].Properties.Add(propertyInfo);
            }
            else
                throw new NotSupportedException($"Unsupported MemberInfo Type: {memberInfo.MemberType}");

            return true;
        }

        // TODO: Find a cleaner way of keeping the assembly and handlers for testing purposes
        public static Assembly Assembly { get; set; }

        public static IEnumerable<Type> DynamicHandlers  { get; set; }

        public DynamicSyncRegistry(IObjectManager objectManager, DynamicSyncPatchProcessor dynamicSyncPatchProcessor)
        {
            this.objectManager = objectManager;
            this.dynamicSyncPatchProcessor = dynamicSyncPatchProcessor;
        }

        public void Build()
        {
            List<Assembly> assemblies = new List<Assembly>
            {
                Assembly.GetExecutingAssembly(),
            };

            // We need to load different dlls based on the runtime
            // currently the games runs .netframework 4.7.2
            // but tests uses .net 6.0
            if(System.Environment.Version.Major <= 4)
            {
                assemblies.Add(typeof(System.Collections.ArrayList).Assembly);
                assemblies.Add(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly);
                assemblies.Add(typeof(System.Linq.Enumerable).Assembly);
                assemblies.Add(typeof(Queue<>).Assembly);
                assemblies.Add(typeof(Console).Assembly);
            }
            else
            {
                assemblies.Add(typeof(System.Linq.Enumerable).Assembly);
                assemblies.Add(typeof(Queue<>).Assembly);
                assemblies.Add(Assembly.GetExecutingAssembly());
                assemblies.Add(Assembly.Load("System.Runtime"));
                assemblies.Add(Assembly.Load("System.Private.CoreLib"));
                assemblies.Add(Assembly.Load("System.Collections"));
                assemblies.Add(typeof(Console).Assembly);

            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                assemblies.Add(Assembly.Load(asm.FullName));
            }

            List<DynamicPatchInfo> dynamicPatches = new List<DynamicPatchInfo>();

            //foreach (var registration in Registrations)
            //{
            //    dynamicPatches.Add(GetPatchInfo(registration.Key, registration.Value, objectManager));
            //}


            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

            if (Directory.Exists($@"{DebugPath}"))
                Directory.Delete($@"{DebugPath}", true);

            var assemblyInfoTemplate = TemplateParser.Parse("DynamicAssemblyInfoTemplate", new
            {
                Assemblies = GetIgnoresAccessChecksToAssemblies(dynamicPatches)
            });

            foreach (var dynamicPatch in dynamicPatches)
            {
                if (!Directory.Exists($@"{DebugPath}\{dynamicPatch.DeclaringType.Name}"))
                    Directory.CreateDirectory($@"{DebugPath}\{dynamicPatch.DeclaringType.Name}");
                syntaxTrees.AddRange(dynamicSyncPatchProcessor.ProcessPatch(dynamicPatch));
            }
            File.WriteAllText($@"{DebugPath}\AssemblyInfo.cs", assemblyInfoTemplate);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(assemblyInfoTemplate));

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

            using (var assemblyStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            {
                var result = dynamicAssembly.Emit(assemblyStream, pdbStream);

                if (!result.Success)
                {
                    throw new InvalidOperationException();
                }
                else
                {
                    // Register all Messages with the SerializationTypeMapper
                    Assembly = Assembly.Load(assemblyStream.GetBuffer());
                    DynamicHandlers = GetDynamicHandlerClasses(Assembly);
                }
            }
        }


        public bool TryGetIntercept(FieldInfo fieldInfo, out MethodInfo intercept, DynamicMessageAction dynamicMessageAction = DynamicMessageAction.Set)
        {
            intercept = null;
            if (dynamicMessageAction == DynamicMessageAction.None)
                throw new InvalidOperationException("Not allowed Intercept Access");
            
            if (!Registrations.TryGetValue(fieldInfo.DeclaringType, out var registryItem))
                return false;
            
            var member = registryItem.Fields.FirstOrDefault(m => m == fieldInfo);
            if (member == null)
                return false;

            var dynamicPatch = Assembly.GetType($"DynamicSync.{fieldInfo.DeclaringType.Name}DynamicPatches");
            if (dynamicPatch == null)
                return false;

            var genericPatch = dynamicPatch.BaseType;

            if(dynamicMessageAction == DynamicMessageAction.Set)
            {
                var messageType = Assembly.GetType($"DynamicSync.{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetMessage");
                var fieldIntercept = genericPatch.GetMethod("FieldIntercept").MakeGenericMethod(fieldInfo.FieldType, messageType);
                intercept = fieldIntercept;
            }

            // TODO: Add Intercept for other actions like adding elements to collection

            return true;
        }


        // TODO: Add TryGetIntercept for properties as above without the set part

        private IEnumerable<Type> GetDynamicHandlerClasses(Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t => t.GetInterface(nameof(IHandler)) != null &&
                            t.IsClass &&
                            t.IsGenericType == false &&
                            t.IsAbstract == false);
            return types;
        }

        private IEnumerable<string> GetIgnoresAccessChecksToAssemblies(List<DynamicPatchInfo> dynamicPatchInfos)
        {
            var assemblies = dynamicPatchInfos.SelectMany(mi => mi.MemberInfos.Select(memberInfo =>
            {
                if (memberInfo.MemberInfo is FieldInfo fieldInfo) return fieldInfo.FieldType.Assembly;
                if (memberInfo.MemberInfo is PropertyInfo propertyInfo) return propertyInfo.PropertyType.Assembly;

                throw new NotSupportedException($"{memberInfo.GetType()} is not supported by {nameof(DynamicSyncRegistry)}");
            }))
            .Concat(dynamicPatchInfos.Select(patchInfo => patchInfo.DeclaringType.Assembly));

            // Allow access from dynamic assembly to private types
            foreach (var assembly in assemblies.Distinct())
            {
                yield return assembly.GetName().Name;
            }
        }

        //private DynamicPatchInfo GetPatchInfo(Type type, DynamicSyncRegistryItem registryItem, IObjectManager objectManager)
        //{
        //    var dynamicPatchInfo = new DynamicPatchInfo
        //    {
        //        DeclaringType = type,
        //        TargetMethods = registryItem.TargetMethods
        //    };
        //    List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

        //    foreach (var member in registryItem.Members)
        //    {
        //        dynamicPatchInfo.MemberInfos.Add(GetDynamicPatchMemberInfo(member, objectManager));
        //    }

        //    List<string> transpilers = new List<string>();
        //    HashSet<string> usings = new HashSet<string>
        //    {
        //        type.Namespace
        //    };

        //    return dynamicPatchInfo;
        //}

        //private DynamicPatchMemberInfo GetDynamicPatchMemberInfo(MemberInfo member, IObjectManager objectManager)
        //{
        //    var patchMemberInfo = new DynamicPatchMemberInfo
        //    {
        //        MemberInfo = member,
        //    };

        //    Type memberType;
        //    bool isField = false;
        //    if(member is FieldInfo fieldInfo)
        //    {
        //        memberType = fieldInfo.FieldType;
        //        isField = true;
        //        patchMemberInfo.UsingDeclarations.Add(fieldInfo.FieldType.Namespace);
        //    }
        //    else
        //    {
        //        var propertyInfo = (PropertyInfo)member;
        //        memberType = propertyInfo.PropertyType;
        //        patchMemberInfo.UsingDeclarations.Add(propertyInfo.PropertyType.Namespace);
        //    }

        //    // Is collection
        //    bool isObjectMangerType = false;
        //    var messageUsings = patchMemberInfo.UsingDeclarations.ToList();
        //    messageUsings.Add(member.DeclaringType.Namespace);
        //    if (memberType.IsGenericType)
        //    {
        //        var genericType = memberType.GenericTypeArguments[0];
        //        messageUsings.Add(genericType.Namespace);
        //        patchMemberInfo.UsingDeclarations.Add(genericType.Namespace);
        //        isObjectMangerType = objectManager.IsTypeManaged(genericType);
        //        if(typeof(MBList<>).IsAssignableFrom(memberType.GetGenericTypeDefinition()))
        //        {
        //            DynamicMessageType messageType = isObjectMangerType ? DynamicMessageType.ObjectManagerType : DynamicMessageType.ValueType;
        //            messageType |= isField ? DynamicMessageType.Field: DynamicMessageType.Property;

        //            messageType |= DynamicMessageType.MBList;

        //            var setMessage = new DynamicMessageInfo
        //            {
        //                Action = DynamicMessageAction.Set,
        //                Type = messageType,
        //                MessageName = $"{member.DeclaringType.Name}_{member.Name}_SetMessage",
        //                UsingDeclarations = messageUsings,
        //                ClassType = member.DeclaringType,
        //                MemberType = memberType,
        //                MemberName = member.Name
        //            };
                    
        //            var addMessage = new DynamicMessageInfo
        //            {
        //                Action = DynamicMessageAction.CollectionAdd,
        //                Type = messageType,
        //                MessageName = $"{member.DeclaringType.Name}_{member.Name}_AddMessage",
        //                UsingDeclarations = messageUsings,
        //                ClassType = member.DeclaringType,
        //                MemberType = genericType,
        //                MemberName = member.Name
        //            };
                    
        //            var removeMessage = new DynamicMessageInfo
        //            {
        //                Action = DynamicMessageAction.CollectionRemove,
        //                Type = messageType,
        //                MessageName = $"{member.DeclaringType.Name}_{member.Name}_RemoveMessage",
        //                UsingDeclarations = messageUsings,
        //                ClassType = member.DeclaringType,
        //                MemberType = genericType,
        //                MemberName = member.Name
        //            };
        //            patchMemberInfo.MessageInfos.Add(setMessage);
        //            patchMemberInfo.MessageInfos.Add(addMessage);
        //            patchMemberInfo.MessageInfos.Add(removeMessage);
        //            patchMemberInfo.PatchType = isField ? DynamicMemberPatchType.FieldMBList : DynamicMemberPatchType.PropertyMBList;
        //        }
        //        else if(typeof(List<>).IsAssignableFrom(memberType.GetGenericTypeDefinition()))
        //        {
        //            DynamicMessageType messageType = isObjectMangerType ? DynamicMessageType.ObjectManagerType : DynamicMessageType.ValueType;
        //            messageType |= isField ? DynamicMessageType.Field : DynamicMessageType.Property;

        //            messageType |= DynamicMessageType.List;

        //            var setMessage = new DynamicMessageInfo
        //            {
        //                Action = DynamicMessageAction.Set,
        //                Type = messageType,
        //                MessageName = $"{member.DeclaringType.Name}_{member.Name}_SetMessage",
        //                UsingDeclarations = messageUsings,
        //                ClassType = member.DeclaringType,
        //                MemberType = memberType,
        //                MemberName = member.Name
        //            };

        //            var addMessage = new DynamicMessageInfo
        //            {
        //                Action = DynamicMessageAction.CollectionAdd,
        //                Type = messageType,
        //                MessageName = $"{member.DeclaringType.Name}_{member.Name}_AddMessage",
        //                UsingDeclarations = messageUsings,
        //                ClassType = member.DeclaringType,
        //                MemberType = genericType,
        //                MemberName = member.Name
        //            };

        //            var removeMessage = new DynamicMessageInfo
        //            {
        //                Action = DynamicMessageAction.CollectionRemove,
        //                Type = messageType,
        //                MessageName = $"{member.DeclaringType.Name}_{member.Name}_RemoveMessage",
        //                UsingDeclarations = messageUsings,
        //                ClassType = member.DeclaringType,
        //                MemberType = genericType,
        //                MemberName = member.Name
        //            };
        //            patchMemberInfo.MessageInfos.Add(setMessage);
        //            patchMemberInfo.MessageInfos.Add(addMessage);
        //            patchMemberInfo.MessageInfos.Add(removeMessage);
        //            patchMemberInfo.PatchType = isField ? DynamicMemberPatchType.FieldList : DynamicMemberPatchType.PropertyList;

        //        }
        //        else if (typeof(Queue<>).IsAssignableFrom(memberType.GetGenericTypeDefinition()))
        //        {
        //            DynamicMessageType messageType = isObjectMangerType ? DynamicMessageType.ObjectManagerType : DynamicMessageType.ValueType;
        //            messageType |= isField ? DynamicMessageType.Field : DynamicMessageType.Property;
        //            messageType |= DynamicMessageType.Queue;

        //            var setMessage = new DynamicMessageInfo
        //            {
        //                Action = DynamicMessageAction.Set,
        //                Type = messageType,
        //                MessageName = $"{member.DeclaringType.Name}_{member.Name}_SetMessage",
        //                UsingDeclarations = messageUsings,
        //                ClassType = member.DeclaringType,
        //                MemberType = memberType,
        //                MemberName = member.Name
        //            };

        //            var addMessage = new DynamicMessageInfo
        //            {
        //                Action = DynamicMessageAction.CollectionAdd,
        //                Type = messageType,
        //                MessageName = $"{member.DeclaringType.Name}_{member.Name}_AddMessage",
        //                UsingDeclarations = messageUsings,
        //                ClassType = member.DeclaringType,
        //                MemberType = genericType,
        //                MemberName = member.Name
        //            };

        //            var removeMessage = new DynamicMessageInfo
        //            {
        //                Action = DynamicMessageAction.CollectionRemove,
        //                Type = messageType,
        //                MessageName = $"{member.DeclaringType.Name}_{member.Name}_RemoveMessage",
        //                UsingDeclarations = messageUsings,
        //                ClassType = member.DeclaringType,
        //                MemberType = genericType,
        //                MemberName = member.Name
        //            };
        //            patchMemberInfo.MessageInfos.Add(setMessage);
        //            patchMemberInfo.MessageInfos.Add(addMessage);
        //            patchMemberInfo.MessageInfos.Add(removeMessage);
        //            patchMemberInfo.PatchType = isField ? DynamicMemberPatchType.FieldQueue : DynamicMemberPatchType.PropertyQueue;
        //        }
        //    }
        //    else if (memberType.IsArray)
        //    {
        //        isObjectMangerType = objectManager.IsTypeManaged(memberType.GetElementType());
        //        messageUsings.Add(memberType.GetElementType().Namespace);
        //        patchMemberInfo.UsingDeclarations.Add(memberType.GetElementType().Namespace);
        //        DynamicMessageType messageType = isObjectMangerType ? DynamicMessageType.ObjectManagerType : DynamicMessageType.ValueType;
        //        messageType |= isField ? DynamicMessageType.Field : DynamicMessageType.Property;
        //        messageType |= DynamicMessageType.Array;

        //        var setMessage = new DynamicMessageInfo
        //        {
        //            Action = DynamicMessageAction.ArraySet,
        //            Type = messageType,
        //            MessageName = $"{member.DeclaringType.Name}_{member.Name}_SetMessage",
        //            UsingDeclarations = messageUsings,
        //            ClassType = member.DeclaringType,
        //            MemberType = memberType,
        //            MemberName = member.Name
        //        };

        //        var changeMessage = new DynamicMessageInfo
        //        {
        //            Action = DynamicMessageAction.ArrayChange,
        //            Type = messageType,
        //            MessageName = $"{member.DeclaringType.Name}_{member.Name}_ChangeMessage",
        //            UsingDeclarations = messageUsings,
        //            ClassType = member.DeclaringType,
        //            MemberType = memberType.GetElementType(),
        //            MemberName = member.Name
        //        };
        //        patchMemberInfo.MessageInfos.Add(setMessage);
        //        patchMemberInfo.MessageInfos.Add(changeMessage);
        //        patchMemberInfo.PatchType = isField ? DynamicMemberPatchType.FieldArray : DynamicMemberPatchType.PropertyArray;
        //    }
        //    else
        //    {
        //        isObjectMangerType = objectManager.IsTypeManaged(memberType);
        //        DynamicMessageType messageType = isObjectMangerType ? DynamicMessageType.ObjectManagerType : DynamicMessageType.ValueType;
        //        messageType |= isField ? DynamicMessageType.Field : DynamicMessageType.Property;

        //        messageType |= DynamicMessageType.Direct;

        //        var setMessage = new DynamicMessageInfo
        //        {
        //            Action = DynamicMessageAction.Set,
        //            Type = messageType,
        //            MessageName = $"{member.DeclaringType.Name}_{member.Name}_SetMessage",
        //            UsingDeclarations = messageUsings,
        //            ClassType = member.DeclaringType,
        //            MemberType = memberType,
        //            MemberName = member.Name
        //        };
        //        patchMemberInfo.MessageInfos.Add(setMessage);
        //        patchMemberInfo.PatchType = isField ? DynamicMemberPatchType.Field : DynamicMemberPatchType.Property;
        //    }

        //    return patchMemberInfo;
        //}
    }
}
