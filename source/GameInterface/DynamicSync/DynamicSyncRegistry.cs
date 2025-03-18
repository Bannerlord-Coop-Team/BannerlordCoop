using GameInterface.Services.ObjectManager;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.Library;

namespace GameInterface.DynamicSync
{
    public class DynamicSyncRegistry
    {
        Dictionary<Type, DynamicSyncRegistryItem> Registrations = new Dictionary<Type, DynamicSyncRegistryItem>();

        string DebugPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\DynamicSyncDebug";

        public bool Add(Type type, MemberInfo memberInfo)
        {
            if (memberInfo is not FieldInfo && memberInfo is not PropertyInfo)
                return false;

            if (!Registrations.ContainsKey(type))
            {
                Registrations.Add(type, new DynamicSyncRegistryItem());
            }

            if (Registrations[type].Members.Contains(memberInfo))
                return false;

            Registrations[type].Members.Add(memberInfo);

            return true;
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


        public Assembly Build(IObjectManager objectManager)
        {
            List<Assembly> assemblies = new List<Assembly>
            {
                Assembly.GetExecutingAssembly(),
                typeof(System.Collections.ArrayList).Assembly,
                typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly,
                typeof(System.Linq.Enumerable).Assembly,
                typeof(Queue<>).Assembly,
                typeof(Console).Assembly
            };
            foreach (var assemblyName in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
            {
                assemblies.Add(Assembly.Load(assemblyName));
            }

            List<DynamicPatchInfo> dynamicPatches = new List<DynamicPatchInfo>();

            foreach (var registration in Registrations)
            {
                dynamicPatches.Add(GetPatchInfo(registration.Key, registration.Value, objectManager));
            }


            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

            if (Directory.Exists($@"{DebugPath}"))
                Directory.Delete($@"{DebugPath}", true);


            string assemblyInfoTemplate = ReadTemplate("DynamicAssemblyInfo.cs");

            List<string> attributes = new List<string>
            {
                $"[assembly: AssemblyVersion(\"1.0.0.0\")]",
                $"[assembly: AssemblyFileVersion(\"1.0.0.0\")]",
            };

            attributes.AddRange(AllowPrivateAccess(dynamicPatches));

            assemblyInfoTemplate = assemblyInfoTemplate.Replace("@Attributes@", string.Join(Environment.NewLine, attributes));

            foreach (var dynamicPatch in dynamicPatches)
            {
                if (!Directory.Exists($@"{DebugPath}\{dynamicPatch.DeclaringType.Name}"))
                    Directory.CreateDirectory($@"{DebugPath}\{dynamicPatch.DeclaringType.Name}");
                syntaxTrees.AddRange(BuildSyntaxTrees(dynamicPatch));
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
                    Console.WriteLine("Do Error handling here");
                    return null;
                }
                else
                {
                    // Register all Messages with the SerializationTypeMapper
                    return Assembly.Load(assemblyStream.GetBuffer());
                }
            }
        }

        private IEnumerable<string> AllowPrivateAccess(List<DynamicPatchInfo> dynamicPatchInfos)
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
                yield return $"[assembly: IgnoresAccessChecksTo(\"{assembly.GetName().Name}\")]";
            }
        }

        private IEnumerable<SyntaxTree> BuildSyntaxTrees(DynamicPatchInfo dynamicPatch)
        {

            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
            HashSet<string> usings = new HashSet<string>
            {
                dynamicPatch.DeclaringType.Namespace
            };

            string patchTemplate = ReadTemplate("DynamicPatchTemplate.cs");

            patchTemplate = patchTemplate
                .Replace("@DynamicPatchClassName@", $"{dynamicPatch.DeclaringType.Name}DynamicPatches")
                .Replace("@TargetType@", $"{dynamicPatch.DeclaringType.Name}");

            List<string> transpilers = new List<string>();
            foreach (var memberPatchInfo in dynamicPatch.MemberInfos)
            {
                foreach (var item in memberPatchInfo.UsingDeclarations)
                {
                    usings.Add(item);
                }

                foreach( var message in memberPatchInfo.MessageInfos)
                {
                    syntaxTrees.Add(GetLocalMessage(message, memberPatchInfo.MemberInfo));
                    syntaxTrees.Add(GetNetworkMessage(message, memberPatchInfo.MemberInfo));
                }

                string transpilerTemplate = GetTranspiler(memberPatchInfo);

                transpilers.Add(transpilerTemplate);
            }

            patchTemplate = patchTemplate
                .Replace("@UsingDeclarations@", string.Join(Environment.NewLine, usings.Select(name => $"using {name};").Distinct()))
                .Replace("@TargetMethods@", "")
                .Replace("@Transpilers@", string.Join(Environment.NewLine + Environment.NewLine, transpilers));

            File.WriteAllText($@"{DebugPath}\{dynamicPatch.DeclaringType.Name}\{dynamicPatch.DeclaringType.Name}Patches.cs", patchTemplate);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(patchTemplate));

            string handlerTemplate = ReadTemplate("DynamicHandlerTemplate.cs");
            List<string> handlerSubscriptions = new List<string>();
            HashSet<string> handlerUsings = new HashSet<string>
            {
                dynamicPatch.DeclaringType.Namespace
            };
            foreach (var message in dynamicPatch.MemberInfos.SelectMany(mi => mi.MessageInfos))
            {
                message.UsingDeclarations.ForEach(u => handlerUsings.Add(u));
                if ((message.Type & DynamicMessageType.Direct) == DynamicMessageType.Direct)
                {
                    if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                    {
                        string subscriptionTemplate = ReadTemplate("Handlers.SubscribeGenericSetReference.cs");
                        subscriptionTemplate = subscriptionTemplate
                                                .Replace("@MemberType@", message.MemberType.Name)
                                                .Replace("@MemberName@", message.MemberName)
                                                .Replace("@MessageType@", message.MessageName)
                                                .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                        handlerSubscriptions.Add(subscriptionTemplate);
                    }
                    else
                    {
                        string subscriptionTemplate = ReadTemplate("Handlers.SubscribeGenericSetValue.cs");
                        subscriptionTemplate = subscriptionTemplate
                                                .Replace("@MemberType@", message.MemberType.Name)
                                                .Replace("@MemberName@", message.MemberName)
                                                .Replace("@MessageType@", message.MessageName)
                                                .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                        handlerSubscriptions.Add(subscriptionTemplate);
                    }
                }
                else if(message.Action == DynamicMessageAction.CollectionAdd && (message.Type & DynamicMessageType.Queue) != DynamicMessageType.Queue)
                {
                    if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                    {
                        string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomNetworkSetReference.cs");
                        subscriptionTemplate = subscriptionTemplate
                                                .Replace("@MemberType@", message.MemberType.Name)
                                                .Replace("@Operation@", $"instance.{message.MemberName}.Add(value)")
                                                .Replace("@MessageType@", message.MessageName)
                                                .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                        handlerSubscriptions.Add(subscriptionTemplate);
                    }
                    else
                    {
                        string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomNetworkSetValue.cs");
                        subscriptionTemplate = subscriptionTemplate
                                                .Replace("@MemberType@", message.MemberType.Name)
                                                .Replace("@Operation@", $"instance.{message.MemberName}.Add(data.Value)")
                                                .Replace("@MessageType@", message.MessageName)
                                                .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                        handlerSubscriptions.Add(subscriptionTemplate);
                    }
                }
                else if (message.Action == DynamicMessageAction.CollectionRemove && (message.Type & DynamicMessageType.Queue) != DynamicMessageType.Queue)
                {
                    if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                    {
                        string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomNetworkSetReference.cs");
                        subscriptionTemplate = subscriptionTemplate
                                                .Replace("@MemberType@", message.MemberType.Name)
                                                .Replace("@Operation@", $"instance.{message.MemberName}.Remove(value)")
                                                .Replace("@MessageType@", message.MessageName)
                                                .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                        handlerSubscriptions.Add(subscriptionTemplate);
                    }
                    else
                    {
                        string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomNetworkSetValue.cs");
                        subscriptionTemplate = subscriptionTemplate
                                                .Replace("@MemberType@", message.MemberType.Name)
                                                .Replace("@Operation@", $"instance.{message.MemberName}.Remove(data.Value)")
                                                .Replace("@MessageType@", message.MessageName)
                                                .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                        handlerSubscriptions.Add(subscriptionTemplate);
                    }

                }
                else if (((message.Type & DynamicMessageType.MBList) == DynamicMessageType.MBList || (message.Type & DynamicMessageType.List) == DynamicMessageType.List) 
                    && message.Action == DynamicMessageAction.Set)
                {
                    string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomSetValue.cs");
                    subscriptionTemplate = subscriptionTemplate
                                            .Replace("@MemberType@", GetMemberType(message.MemberType))
                                            .Replace("@MessageType@", message.MessageName)
                                            .Replace("@NetworkMessageType@", "Network_" + message.MessageName);

                    if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                    {
                        subscriptionTemplate = subscriptionTemplate
                                            .Replace("@NetworkMemberType@", "List<string>")
                                            .Replace("@Operation@", 
                                            $@"{{ 
                                                var valueIds = new List<string>();
                                                for (int i = 0; i < data.Value.Count; i++)
                                                {{
                                                    if (!TryGetId(data.Value[i], out string valueId)) return;
                                                    valueIds.Add(valueId);
                                                }}
                                                network.SendAll(new {"Network_" + message.MessageName}(instanceId, valueIds));
                                            }}")
                                            .Replace("@NetworkOperation@", 
                                            $@"{{
                                                instance.{message.MemberName} = new {GetMemberType(message.MemberType)}();
                                                for (int i = 0; i < data.Value.Count; i++)
                                                {{
                                                    if (!objectManager.TryGetObject(data.Value[i], out {GetGenericType(message.MemberType)} value)) return;
                                                    instance.{message.MemberName}.Add(value);
                                                }}
                                            }}");
                    }
                    else
                    {
                        subscriptionTemplate = subscriptionTemplate
                                            .Replace("@NetworkMemberType@", $"List<{GetGenericType(message.MemberType)}>")
                                            .Replace("@Operation@",
                                            $@"{{
                                                network.SendAll(new {"Network_" + message.MessageName}(instanceId, data.Value.ToList()));
                                            }}")
                                            .Replace("@NetworkOperation@",
                                            $@"{{
                                                instance.{message.MemberName} = new {GetMemberType(message.MemberType)}();
                                                for (int i = 0; i < data.Value.Count; i++)
                                                {{
                                                    instance.{message.MemberName}.Add(value);
                                                }}
                                            }}");
                    }

                    handlerSubscriptions.Add(subscriptionTemplate);
                }
                else if ((message.Type & DynamicMessageType.Queue) == DynamicMessageType.Queue)
                {
                    if (message.Action == DynamicMessageAction.Set)
                    {
                        string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomSetValue.cs");
                        subscriptionTemplate = subscriptionTemplate
                                                .Replace("@MemberType@", GetMemberType(message.MemberType))
                                                .Replace("@MessageType@", message.MessageName)
                                                .Replace("@NetworkMessageType@", "Network_" + message.MessageName);

                        if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                        {
                            subscriptionTemplate = subscriptionTemplate
                                                .Replace("@NetworkMemberType@", "List<string>")
                                                .Replace("@Operation@",
                                                $@"{{ 
                                                var valueIds = new List<string>();
                                                for (int i = 0; i < data.Value.Count; i++)
                                                {{
                                                    if (!TryGetId(data.Value.ElementAt(i), out string valueId)) return;
                                                    valueIds.Add(valueId);
                                                }}
                                                network.SendAll(new {"Network_" + message.MessageName}(instanceId, valueIds));
                                            }}")
                                                .Replace("@NetworkOperation@",
                                                $@"{{
                                                instance.{message.MemberName} = new {GetMemberType(message.MemberType)}();
                                                for (int i = 0; i < data.Value.Count; i++)
                                                {{
                                                    if (!objectManager.TryGetObject(data.Value[i], out {GetGenericType(message.MemberType)} value)) return;
                                                    instance.{message.MemberName}.Enqueue(value);
                                                }}
                                            }}");
                        }
                        else
                        {
                            subscriptionTemplate = subscriptionTemplate
                                                .Replace("@NetworkMemberType@", $"List<{GetGenericType(message.MemberType)}>")
                                                .Replace("@Operation@",
                                                $@"{{
                                                network.SendAll(new {"Network_" + message.MessageName}(instanceId, data.Value.ToList()));
                                            }}")
                                                .Replace("@NetworkOperation@",
                                                $@"{{
                                                instance.{message.MemberName} = new {GetMemberType(message.MemberType)}();
                                                for (int i = 0; i < data.Value.Count; i++)
                                                {{
                                                    instance.{message.MemberName}.Add(value);
                                                }}
                                            }}");
                        }

                        handlerSubscriptions.Add(subscriptionTemplate);
                    }
                    else if (message.Action == DynamicMessageAction.CollectionAdd)
                    {
                        if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                        {
                            string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomNetworkSetReference.cs");
                            subscriptionTemplate = subscriptionTemplate
                                                    .Replace("@MemberType@", message.MemberType.Name)
                                                    .Replace("@Operation@", $"instance.{message.MemberName}.Enqueue(value)")
                                                    .Replace("@MessageType@", message.MessageName)
                                                    .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                            handlerSubscriptions.Add(subscriptionTemplate);
                        }
                        else
                        {
                            string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomNetworkSetValue.cs");
                            subscriptionTemplate = subscriptionTemplate
                                                    .Replace("@MemberType@", message.MemberType.Name)
                                                    .Replace("@Operation@", $"instance.{message.MemberName}.Enqueue(data.Value)")
                                                    .Replace("@MessageType@", message.MessageName)
                                                    .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                            handlerSubscriptions.Add(subscriptionTemplate);
                        }
                    }
                    else if (message.Action == DynamicMessageAction.CollectionRemove)
                    {
                        if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                        {
                            string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomNetworkSetReference.cs");
                            subscriptionTemplate = subscriptionTemplate
                                                    .Replace("@MemberType@", message.MemberType.Name)
                                                    .Replace("@Operation@", $"instance.{message.MemberName}.Dequeue()")
                                                    .Replace("@MessageType@", message.MessageName)
                                                    .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                            handlerSubscriptions.Add(subscriptionTemplate);
                        }
                        else
                        {
                            string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomNetworkSetValue.cs");
                            subscriptionTemplate = subscriptionTemplate
                                                    .Replace("@MemberType@", message.MemberType.Name)
                                                    .Replace("@Operation@", $"instance.{message.MemberName}.Dequeue()")
                                                    .Replace("@MessageType@", message.MessageName)
                                                    .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                            handlerSubscriptions.Add(subscriptionTemplate);
                        }
                    }
                }

                else if ((message.Type & DynamicMessageType.Array) == DynamicMessageType.Array)
                {
                    if (message.Action == DynamicMessageAction.ArraySet)
                    {
                        string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomSetValue.cs");
                        subscriptionTemplate = subscriptionTemplate
                                                .Replace("@MemberType@", message.MemberType.Name)
                                                .Replace("@MessageType@", message.MessageName)
                                                .Replace("@NetworkMessageType@", "Network_" + message.MessageName);

                        if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                        {
                            subscriptionTemplate = subscriptionTemplate
                                                .Replace("@NetworkMemberType@", "List<(int Index, string Id)>")
                                                .Replace("@Operation@",
                                                $@"{{ 
                                                var valueIds = new List<(int Index, string Id)>();
                                                for (int i = 0; i < data.Value.Length; i++)
                                                {{
                                                    if(data.Value[i] == null) continue;
                                                    if (!TryGetId(data.Value[i], out string valueId)) return;
                                                    valueIds.Add(new (i,valueId));
                                                }}
                                                network.SendAll(new {"Network_" + message.MessageName}(instanceId, valueIds, data.Value.Length));
                                            }}")
                                                .Replace("@NetworkOperation@",
                                                $@"{{
                                                instance.{message.MemberName} = new {message.MemberType.GetElementType().Name}[data.Length];
                                                for (int i = 0; i < data.Value.Count; i++)
                                                {{
                                                    if (!objectManager.TryGetObject(data.Value[i].Id, out {message.MemberType.GetElementType().Name} value)) return;
                                                    instance.{message.MemberName}[data.Value[i].Index] = value;
                                                }}
                                            }}");
                        }
                        else
                        {
                            subscriptionTemplate = subscriptionTemplate
                                                .Replace("@NetworkMemberType@", "List<(int Index, string Id)>")
                                                .Replace("@Operation@",
                                                $@"{{ 
                                                var valueIds = new List<(int Index, string Id)>();
                                                for (int i = 0; i < data.Value.Length; i++)
                                                {{
                                                    if(data.Value[i] == null) continue;
                                                    valueIds.Add(new (i, data.Value[i]));
                                                }}
                                                network.SendAll(new {"Network_" + message.MessageName}(instanceId, valueIds, data.Value.Length));
                                            }}")
                                                .Replace("@NetworkOperation@",
                                                $@"{{
                                                instance.{message.MemberName} = new {message.MemberType.GetElementType().Name}[data.Length];
                                                for (int i = 0; i < data.Value.Count; i++)
                                                {{
                                                    instance.{message.MemberName}[data.Value[i].Index] = data.Value[i];
                                                }}
                                            }}");
                        }
                        handlerSubscriptions.Add(subscriptionTemplate);
                    }

                    else if (message.Action == DynamicMessageAction.ArrayChange)
                    {
                        if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                        {
                            string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomSetValue.cs");
                            subscriptionTemplate = subscriptionTemplate
                                                    .Replace("@MemberType@", message.MemberType.Name)
                                                    .Replace("@NetworkMemberType@", message.MemberType.Name)
                                                    .Replace("@Operation@", $"{{if (!TryGetId(data.Value, out string valueId) && data.Value != null) return;network.SendAll(new {"Network_" + message.MessageName}(instanceId, valueId, data.Index));}}")
                                                    .Replace("@NetworkOperation@", $"{{if (!objectManager.TryGetObject(data.ValueId, out {message.MemberType.Name} value) && data.ValueId != null) return; instance.{message.MemberName}[data.Index] = value;}}")
                                                    .Replace("@MessageType@", message.MessageName)
                                                    .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                            handlerSubscriptions.Add(subscriptionTemplate);
                        }
                        else
                        {
                            string subscriptionTemplate = ReadTemplate("Handlers.SubscribeCustomSetValue.cs");
                            subscriptionTemplate = subscriptionTemplate
                                                    .Replace("@MemberType@", message.MemberType.Name)
                                                    .Replace("@NetworkMemberType@", message.MemberType.Name)
                                                    .Replace("@Operation@", $"network.SendAll(new {"Network_" + message.MessageName}(instanceId, data.Value, data.Index));")
                                                    .Replace("@NetworkOperation@", $"instance.{message.MemberName}[data.Index] = data.Value")
                                                    .Replace("@MessageType@", message.MessageName)
                                                    .Replace("@NetworkMessageType@", "Network_" + message.MessageName);
                            handlerSubscriptions.Add(subscriptionTemplate);
                        }
                    }
                }
            }

            handlerTemplate = handlerTemplate
                .Replace("@UsingDeclarations@", string.Join(Environment.NewLine, usings.Select(name => $"using {name};").Distinct()))
                .Replace("@Subscriptions@", string.Join(Environment.NewLine, handlerSubscriptions))
                .Replace("@HandlerType@", $"Dynamic{dynamicPatch.DeclaringType.Name}Handler")
                .Replace("@ClassType@", dynamicPatch.DeclaringType.Name);

            File.WriteAllText($@"{DebugPath}\{dynamicPatch.DeclaringType.Name}\Dynamic{dynamicPatch.DeclaringType.Name}Handler.cs", handlerTemplate);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(handlerTemplate));
            return syntaxTrees;
        }

        private SyntaxTree GetNetworkMessage(DynamicMessageInfo message, MemberInfo memberInfo)
        {
            string messageTemplate = string.Empty;
            if((message.Type & DynamicMessageType.Direct) == DynamicMessageType.Direct)
            {
                if ((message.Type & DynamicMessageType.ValueType) == DynamicMessageType.ValueType)
                {
                    messageTemplate = ReadTemplate($"Messages.GenericNetworkEvent.cs");
                }
                else
                {
                    messageTemplate = ReadTemplate($"Messages.GenericNetworkReferenceEvent.cs");
                }
            }
            else if((message.Type & DynamicMessageType.List) == DynamicMessageType.List ||
                (message.Type & DynamicMessageType.MBList) == DynamicMessageType.MBList ||
                (message.Type & DynamicMessageType.Queue) == DynamicMessageType.Queue)
            {
                if(message.Action == DynamicMessageAction.CollectionAdd || message.Action == DynamicMessageAction.CollectionRemove)
                {
                    if((message.Type & DynamicMessageType.ValueType) == DynamicMessageType.ValueType)
                    {
                        messageTemplate = ReadTemplate($"Messages.GenericNetworkEvent.cs");
                    }
                    else
                    {
                        messageTemplate = ReadTemplate($"Messages.GenericNetworkReferenceEvent.cs");
                    }
                }
                else if(message.Action == DynamicMessageAction.Set)
                {


                    if ((message.Type & DynamicMessageType.ValueType) == DynamicMessageType.ValueType)
                    {
                        // Use member for initial size
                        messageTemplate = ReadTemplate($"Messages.GenericNetworkEvent.cs")
                                            .Replace("@MemberType@", $"List<{GetGenericType(message.MemberType)}>");
                    }
                    else
                    {
                        // Use member for initial size
                        messageTemplate = ReadTemplate($"Messages.GenericNetworkEvent.cs")
                                            .Replace("@MemberType@", $"List<string>");
                    }
                }
            }
            else if ((message.Type & DynamicMessageType.Array) == DynamicMessageType.Array)
            {
                if(message.Action == DynamicMessageAction.ArrayChange)
                {
                    if ((message.Type & DynamicMessageType.ValueType) == DynamicMessageType.ValueType)
                    {
                        messageTemplate = ReadTemplate($"Messages.GenericNetworkArrayChangedEvent.cs");
                    }
                    else
                    {
                        messageTemplate = ReadTemplate($"Messages.GenericNetworkArrayChangedReferenceEvent.cs");
                    }
                }
                else if(message.Action == DynamicMessageAction.ArraySet)
                {
                    messageTemplate = ReadTemplate($"Messages.GenericNetworkArraySetEvent.cs");
                    if ((message.Type & DynamicMessageType.ValueType) == DynamicMessageType.ValueType)
                    {
                        // Use member for initial size
                        messageTemplate = messageTemplate
                                            .Replace("@MemberType@", $"List<{message.MemberType.GetElementType()}>");
                    }
                    else
                    {
                        // Use member for initial size
                        messageTemplate = messageTemplate
                                            .Replace("@MemberType@", $"List<(int Index, string Id)>");
                    }
                }
            }

            messageTemplate = messageTemplate.Replace("@ClassType@", message.ClassType.Name)
                .Replace("@MemberType@", $"{GetMemberType(message.MemberType)}")
                .Replace("@MessageType@", $"Network_{message.MessageName}")
                .Replace("@UsingDeclarations@", string.Join(Environment.NewLine, message.UsingDeclarations.Select(name => $"using {name};").Distinct()));

            File.WriteAllText($@"{DebugPath}\{memberInfo.DeclaringType.Name}\Network_{message.MessageName}.cs", messageTemplate);
            return CSharpSyntaxTree.ParseText(messageTemplate);
        }

        private SyntaxTree GetLocalMessage(DynamicMessageInfo message, MemberInfo memberInfo)
        {
            string messageTemplate;
            if(message.Action != DynamicMessageAction.ArrayChange)
                messageTemplate = ReadTemplate($"Messages.GenericMessageTemplate.cs");
            else
                messageTemplate = ReadTemplate($"Messages.GenericArrayChangedMessageTemplate.cs");

            messageTemplate = messageTemplate
                .Replace("@ClassType@", $"{message.ClassType.Name}")
                .Replace("@MemberType@", $"{GetMemberType(message.MemberType)}")
                .Replace("@MessageType@", message.MessageName)
                .Replace("@UsingDeclarations@", string.Join(Environment.NewLine, message.UsingDeclarations.Select(name => $"using {name};").Distinct()));

            // Debug
            File.WriteAllText($@"{DebugPath}\{memberInfo.DeclaringType.Name}\{message.MessageName}.cs", messageTemplate);
            return CSharpSyntaxTree.ParseText(messageTemplate);
        }

        private string GetTranspiler(DynamicPatchMemberInfo memberPatchInfo)
        {
            Type targetType;
            if (memberPatchInfo.MemberInfo is FieldInfo fieldInfo)
            {
                targetType = fieldInfo.FieldType;
            }
            else
            {
                var propertyInfo = (PropertyInfo)memberPatchInfo.MemberInfo;
                targetType = propertyInfo.PropertyType;
            }
            if (targetType.IsGenericType)
            {
                targetType = targetType.GenericTypeArguments[0];
            }
            else if (targetType.IsArray)
                targetType = targetType.GetElementType();

            string transpilerTemplate = ReadTemplate($"Transpilers.{memberPatchInfo.TranspilerType.ToString()}TranspilerTemplate.cs");
            transpilerTemplate = transpilerTemplate
                .Replace("@MemberName@", $"{memberPatchInfo.MemberInfo.Name}")
                .Replace("@MemberType@", $"{GetMemberType(targetType)}")
                .Replace("@MessageTypes@", $"{string.Join(",", memberPatchInfo.MessageInfos.Select(m => m.MessageName))}");
            return transpilerTemplate;
        }
        private string GetMemberType(Type targetType)
        {
            string memberType = targetType.Name;
            if (targetType.IsGenericType)
            {
                memberType = targetType.Name.Trim('`', '1') + "<" + targetType.GenericTypeArguments[0].Name + ">";
            }
            return memberType;
        }

        private string GetGenericType(Type targetType)
        {
            return targetType.GenericTypeArguments[0].Name;
        }

        private DynamicPatchInfo GetPatchInfo(Type type, DynamicSyncRegistryItem registryItem, IObjectManager objectManager)
        {
            var dynamicPatchInfo = new DynamicPatchInfo
            {
                DeclaringType = type,
                TargetMethods = registryItem.TargetMethods
            };
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

            foreach (var member in registryItem.Members)
            {
                dynamicPatchInfo.MemberInfos.Add(GetDynamicPatchMemberInfo(member, objectManager));
            }

            List<string> transpilers = new List<string>();
            HashSet<string> usings = new HashSet<string>
            {
                type.Namespace
            };

            return dynamicPatchInfo;
        }

        private DynamicPatchMemberInfo GetDynamicPatchMemberInfo(MemberInfo member, IObjectManager objectManager)
        {
            var patchMemberInfo = new DynamicPatchMemberInfo
            {
                MemberInfo = member,
            };

            Type memberType;
            bool isField = false;
            if(member is FieldInfo fieldInfo)
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
                if(typeof(MBList<>).IsAssignableFrom(memberType.GetGenericTypeDefinition()))
                {
                    DynamicMessageType messageType = isObjectMangerType ? DynamicMessageType.ObjectManagerType : DynamicMessageType.ValueType;
                    messageType |= isField ? DynamicMessageType.Field: DynamicMessageType.Property;

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
                    patchMemberInfo.TranspilerType = isField ? DynamicTranspilerType.FieldMBList : DynamicTranspilerType.PropertyMBList;
                }
                else if(typeof(List<>).IsAssignableFrom(memberType.GetGenericTypeDefinition()))
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
                    patchMemberInfo.TranspilerType = isField ? DynamicTranspilerType.FieldList : DynamicTranspilerType.PropertyList;

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
                    patchMemberInfo.TranspilerType = isField ? DynamicTranspilerType.FieldQueue : DynamicTranspilerType.PropertyQueue;
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
                patchMemberInfo.TranspilerType = isField ? DynamicTranspilerType.FieldArray : DynamicTranspilerType.PropertyArray;
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
                patchMemberInfo.TranspilerType = isField ? DynamicTranspilerType.Field : DynamicTranspilerType.Property;
            }

            return patchMemberInfo;
        }

        public string ReadTemplate(string templateName)
        {
            // Determine path
            var assembly = Assembly.GetExecutingAssembly();
            string resourcePath = $"GameInterface.DynamicSync.Templates.{templateName}";
            using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

    }
}
