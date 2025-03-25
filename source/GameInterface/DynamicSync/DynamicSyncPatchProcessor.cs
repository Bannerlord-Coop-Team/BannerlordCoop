using GameInterface.DynamicSync.Templates;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.Diamond.Rest;

namespace GameInterface.DynamicSync
{
    public class DynamicSyncPatchProcessor
    {
        private struct HandlerData
        {
            public string MemberType;
            public string MemberName;
            public string MessageType;
            public string Operation;
            public string NetworkMessageType;
            public string NetworkMemberType;
            public string NetworkOperation;
        }

        private struct MessageData
        {
            public string MemberType;
            public string MemberName;
            public string MessageType;
            public string Operation;
            public string NetworkMessageType;
            public string NetworkMemberType;
            public string NetworkOperation;
        }

        string DebugPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\DynamicSyncDebug";

        public List<SyntaxTree> ProcessPatch(DynamicPatchInfo dynamicPatch)
        {
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
            syntaxTrees.AddRange(GetDynamicHandler(dynamicPatch));
            syntaxTrees.Add(GetDynamicPatch(dynamicPatch));
            return syntaxTrees;
        }

        private IEnumerable<SyntaxTree> GetDynamicHandler(DynamicPatchInfo dynamicPatch)
        {
            HashSet<string> usings = new HashSet<string>
            {
                dynamicPatch.DeclaringType.Namespace
            };
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
            List<string> handlerSubscriptions = new List<string>();
            foreach (var memberPatchInfo in dynamicPatch.MemberInfos)
            {
                foreach (var item in memberPatchInfo.UsingDeclarations)
                {
                    usings.Add(item);
                }
                foreach (var message in memberPatchInfo.MessageInfos)
                {
                    message.UsingDeclarations.ForEach(u => usings.Add(u));
                    syntaxTrees.Add(GetLocalMessage(message, memberPatchInfo.MemberInfo));
                    syntaxTrees.Add(GetNetworkMessage(message, memberPatchInfo.MemberInfo));
                    handlerSubscriptions.Add(GetMessageSubscription(message, memberPatchInfo.MemberInfo));
                }
            }

            string handlerTemplate = TemplateParser.Parse("Handlers.DynamicHandlerTemplate",
                new
                {
                    Libraries = usings.Distinct().ToList(),
                    Subscriptions = handlerSubscriptions,
                    HandlerType = $"Dynamic{dynamicPatch.DeclaringType.Name}Handler",
                    DeclaringType = dynamicPatch.DeclaringType.Name
                });

            File.WriteAllText($@"{DebugPath}\{dynamicPatch.DeclaringType.Name}\Dynamic{dynamicPatch.DeclaringType.Name}Handler.cs", handlerTemplate);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(handlerTemplate));
            return syntaxTrees;
        }

        #region DynamicPatch
        private SyntaxTree GetDynamicPatch(DynamicPatchInfo dynamicPatch)
        {
            List<string> transpilers = new List<string>();
            List<string> prefixes = new List<string>();
            HashSet<string> usings = new HashSet<string>
            {
                dynamicPatch.DeclaringType.Namespace
            };

            foreach(var methodInfo in dynamicPatch.TargetMethods)
            {
                usings.Add(methodInfo.DeclaringType.Namespace);
            }

            foreach (var memberPatchInfo in dynamicPatch.MemberInfos)
            {
                foreach (var item in memberPatchInfo.UsingDeclarations)
                {
                    usings.Add(item);
                }
                var template = GetMemberPatches(memberPatchInfo);

                transpilers.AddRange(template.transpilers);
                prefixes.AddRange(template.prefixes);
            }

            string patchTemplate = TemplateParser.Parse("Patches.DynamicPatchTemplate",
                new
                {
                    Libraries = usings.Distinct().ToList(),
                    DynamicPatchName = $"{dynamicPatch.DeclaringType.Name}DynamicPatches",
                    TargetType = dynamicPatch.DeclaringType.Name,
                    Transpilers = transpilers,
                    Prefixes = prefixes,
                    TargetMethods = dynamicPatch.TargetMethods.Select(tm => new
                    {
                        DeclaringType = tm.DeclaringType.Name,
                        Name = tm.Name
                    }).ToList()
                });

            File.WriteAllText($@"{DebugPath}\{dynamicPatch.DeclaringType.Name}\{dynamicPatch.DeclaringType.Name}DynamicPatches.cs", patchTemplate);
            return CSharpSyntaxTree.ParseText(patchTemplate);
        }

        private (List<string> transpilers, List<string> prefixes) GetMemberPatches(DynamicPatchMemberInfo memberPatchInfo)
        {
            Type targetType;
            string baseTemplate = "";
            (List<string> transpilers, List<string> prefixes) result = new()
            {
                transpilers = new List<string>(),
                prefixes = new List<string>()
            };
            if (memberPatchInfo.MemberInfo is FieldInfo fieldInfo)
            {
                targetType = fieldInfo.FieldType;
                baseTemplate = "FieldSetTranspilerTemplate";
                result.transpilers.Add(TemplateParser.Parse($"Patches.{baseTemplate}",
                new
                {
                    MemberName = memberPatchInfo.MemberInfo.Name,
                    MemberType = GetMemberType(targetType),
                    MessageType = memberPatchInfo.MessageInfos.First(mi => mi.Action == DynamicMessageAction.Set || mi.Action == DynamicMessageAction.ArraySet).MessageName,
                }));
            }
            else
            {
                var propertyInfo = (PropertyInfo)memberPatchInfo.MemberInfo;
                targetType = propertyInfo.PropertyType;

                // Only create setter prefix if the property can actually change
                if (propertyInfo.CanWrite)
                {
                    result.prefixes.Add(TemplateParser.Parse($"Patches.PropertySetPrefixTemplate",
                    new
                    {
                        MemberName = memberPatchInfo.MemberInfo.Name,
                        MemberType = GetMemberType(targetType),
                        MessageType = memberPatchInfo.MessageInfos.First(mi => mi.Action == DynamicMessageAction.Set || mi.Action == DynamicMessageAction.ArraySet).MessageName,
                        MemberDeclaringType = memberPatchInfo.MemberInfo.DeclaringType.Name // Only used for PropertyPrefix
                    }));
                }
            }

            if (targetType.IsGenericType || targetType.IsArray)
            {
                if (targetType.IsGenericType)
                {
                    var genericType = targetType.GenericTypeArguments[0];
                    var templateData = new
                    {
                        MemberName = memberPatchInfo.MemberInfo.Name,
                        MemberType = GetMemberType(genericType),
                        AddMessageType = memberPatchInfo.MessageInfos.First(mi => mi.Action == DynamicMessageAction.CollectionAdd).MessageName,
                        RemoveMessageType = memberPatchInfo.MessageInfos.First(mi => mi.Action == DynamicMessageAction.CollectionRemove).MessageName
                    };

                    if (memberPatchInfo.PatchType == DynamicMemberPatchType.FieldList)
                        result.transpilers.Add(TemplateParser.Parse("Patches.FieldListChangeTranspilerTemplate", templateData));
                    else if (memberPatchInfo.PatchType == DynamicMemberPatchType.PropertyList)
                        result.transpilers.Add(TemplateParser.Parse("Patches.PropertyListChangeTranspilerTemplate", templateData));
                    else if (memberPatchInfo.PatchType == DynamicMemberPatchType.FieldMBList)
                        result.transpilers.Add(TemplateParser.Parse("Patches.FieldMBListChangeTranspilerTemplate", templateData));
                    else if (memberPatchInfo.PatchType == DynamicMemberPatchType.PropertyMBList)
                        result.transpilers.Add(TemplateParser.Parse("Patches.PropertyMBListChangeTranspilerTemplate", templateData));
                    else if (memberPatchInfo.PatchType == DynamicMemberPatchType.FieldQueue)
                        result.transpilers.Add(TemplateParser.Parse("Patches.FieldQueueChangeTranspilerTemplate", templateData));
                    else if (memberPatchInfo.PatchType == DynamicMemberPatchType.PropertyQueue)
                        result.transpilers.Add(TemplateParser.Parse("Patches.PropertyQueueChangeTranspilerTemplate", templateData));
                }
                else
                {
                    var genericType = targetType.GetElementType();
                    var templateData = new
                    {
                        MemberName = memberPatchInfo.MemberInfo.Name,
                        MemberType = GetMemberType(genericType),
                        ChangeMessageType = memberPatchInfo.MessageInfos.First(mi => mi.Action == DynamicMessageAction.ArrayChange).MessageName,
                    };

                    if (memberPatchInfo.PatchType == DynamicMemberPatchType.FieldArray)
                        result.transpilers.Add(TemplateParser.Parse("Patches.FieldArrayChangeTranspilerTemplate", templateData));
                    else if (memberPatchInfo.PatchType == DynamicMemberPatchType.PropertyArray)
                        result.transpilers.Add(TemplateParser.Parse("Patches.PropertyArrayChangeTranspilerTemplate", templateData));
                }
            }
            return result;
        }
        #endregion
        #region Messages
        private SyntaxTree GetLocalMessage(DynamicMessageInfo message, MemberInfo memberInfo)
        {
            string messageTemplate;
            if (message.Action != DynamicMessageAction.ArrayChange)
                messageTemplate = "Messages.GenericMessageTemplate";
            else
                messageTemplate = "Messages.GenericArrayChangedMessageTemplate";

            messageTemplate = TemplateParser.Parse(messageTemplate, new
            {
                MemberDeclaringType = message.ClassType.Name,
                MemberType = GetMemberType(message.MemberType),
                MessageType = message.MessageName,
                Libraries = message.UsingDeclarations.Distinct().ToList()

            });

            // Debug
            File.WriteAllText($@"{DebugPath}\{memberInfo.DeclaringType.Name}\{message.MessageName}.cs", messageTemplate);
            return CSharpSyntaxTree.ParseText(messageTemplate);
        }

        private SyntaxTree GetNetworkMessage(DynamicMessageInfo message, MemberInfo memberInfo)
        {
            string messageTemplate = string.Empty;
            string memberType = $"{GetMemberType(message.MemberType)}";
            if ((message.Type & DynamicMessageType.Direct) == DynamicMessageType.Direct)
            {
                if ((message.Type & DynamicMessageType.ValueType) == DynamicMessageType.ValueType)
                    messageTemplate = "Messages.GenericNetworkEvent";
                else
                    messageTemplate = "Messages.GenericNetworkReferenceEvent";
            }
            else if ((message.Type & DynamicMessageType.List) == DynamicMessageType.List ||
                (message.Type & DynamicMessageType.MBList) == DynamicMessageType.MBList ||
                (message.Type & DynamicMessageType.Queue) == DynamicMessageType.Queue)
            {
                if (message.Action == DynamicMessageAction.CollectionAdd || message.Action == DynamicMessageAction.CollectionRemove)
                {
                    if ((message.Type & DynamicMessageType.ValueType) == DynamicMessageType.ValueType)
                        messageTemplate = "Messages.GenericNetworkEvent";
                    else
                        messageTemplate = "Messages.GenericNetworkReferenceEvent";
                }
                else if (message.Action == DynamicMessageAction.Set)
                {


                    if ((message.Type & DynamicMessageType.ValueType) == DynamicMessageType.ValueType)
                    {
                        messageTemplate = "Messages.GenericNetworkEvent";
                        memberType = $"List<{GetGenericType(message.MemberType)}>";
                    }
                    else
                    {
                        // Use member for initial size
                        messageTemplate = "Messages.GenericNetworkEvent";
                        memberType = $"List<string>";
                    }
                }
            }
            else if ((message.Type & DynamicMessageType.Array) == DynamicMessageType.Array)
            {
                if (message.Action == DynamicMessageAction.ArrayChange)
                {
                    if ((message.Type & DynamicMessageType.ValueType) == DynamicMessageType.ValueType)
                        messageTemplate = "Messages.GenericNetworkArrayChangedEvent";
                    else
                        messageTemplate = "Messages.GenericNetworkArrayChangedReferenceEvent";
                }
                else if (message.Action == DynamicMessageAction.ArraySet)
                {
                    messageTemplate = "Messages.GenericNetworkArraySetEvent";
                    if ((message.Type & DynamicMessageType.ValueType) == DynamicMessageType.ValueType)
                        memberType = $"List<{message.MemberType.GetElementType()}>";
                    else
                        memberType = $"List<(int Index, string Id)>";
                }
            }
            messageTemplate = TemplateParser.Parse(messageTemplate,
                new
                {
                    MemberDeclaringType = message.ClassType.Name,
                    MemberType = memberType,
                    MessageType = $"Network_{message.MessageName}",
                    Libraries = message.UsingDeclarations.Distinct().ToList()
                });
            File.WriteAllText($@"{DebugPath}\{memberInfo.DeclaringType.Name}\Network_{message.MessageName}.cs", messageTemplate);
            return CSharpSyntaxTree.ParseText(messageTemplate);
        }


        private string GetMessageSubscription(DynamicMessageInfo message, MemberInfo memberInfo)
        {
            string subscriptionTemplate;
            var messageData = new HandlerData
            {
                MemberType = message.MemberType.Name,
                MemberName = message.MemberName,
                MessageType = message.MessageName,
                NetworkMessageType = "Network_" + message.MessageName,
                Operation = "",
                NetworkMemberType = "",
                NetworkOperation = ""
            };
            if ((message.Type & DynamicMessageType.Direct) == DynamicMessageType.Direct)
            {
                if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                    subscriptionTemplate = "Handlers.SubscribeGenericSetReference";
                else
                    subscriptionTemplate = "Handlers.SubscribeGenericSetValue";
            }
            else if (message.Action == DynamicMessageAction.CollectionAdd && (message.Type & DynamicMessageType.Queue) != DynamicMessageType.Queue)
            {
                if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                {
                    subscriptionTemplate = "Handlers.SubscribeCustomNetworkSetReference";
                    messageData.Operation = $"instance.{message.MemberName}.Add(value)";

                }
                else
                {
                    subscriptionTemplate = "Handlers.SubscribeCustomNetworkSetValue";
                    messageData.Operation = $"instance.{message.MemberName}.Add(data.Value)";
                }
            }
            else if (message.Action == DynamicMessageAction.CollectionRemove && (message.Type & DynamicMessageType.Queue) != DynamicMessageType.Queue)
            {
                if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                {
                    subscriptionTemplate = "Handlers.SubscribeCustomNetworkSetReference";
                    messageData.Operation = $"instance.{message.MemberName}.Remove(value)";
                }
                else
                {
                    subscriptionTemplate = "Handlers.SubscribeCustomNetworkSetValue";
                    messageData.Operation = $"instance.{message.MemberName}.Remove(data.Value)";
                }
            }
            else if (((message.Type & DynamicMessageType.MBList) == DynamicMessageType.MBList || (message.Type & DynamicMessageType.List) == DynamicMessageType.List)
                && message.Action == DynamicMessageAction.Set)
            {
                subscriptionTemplate = "Handlers.SubscribeCustomSetValue";
                messageData.MemberType = GetMemberType(message.MemberType);

                if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                {
                    messageData.NetworkMemberType = "List<string>";
                    messageData.Operation =
                    $@"{{ 
                            var valueIds = new List<string>();
                            for (int i = 0; i < data.Value.Count; i++)
                            {{
                                if (!TryGetId(data.Value[i], out string valueId)) return;
                                valueIds.Add(valueId);
                            }}
                            network.SendAll(new {"Network_" + message.MessageName}(instanceId, valueIds));
                        }}";
                    messageData.NetworkOperation =
                    $@"{{
                            instance.{message.MemberName} = new {GetMemberType(message.MemberType)}();
                            for (int i = 0; i < data.Value.Count; i++)
                            {{
                                if (!objectManager.TryGetObject(data.Value[i], out {GetGenericType(message.MemberType)} value)) return;
                                instance.{message.MemberName}.Add(value);
                            }}
                        }}";
                }
                else
                {
                    messageData.NetworkMemberType = $"List<{GetGenericType(message.MemberType)}>";
                    messageData.Operation = $@"{{network.SendAll(new {"Network_" + message.MessageName}(instanceId, data.Value.ToList()));}}";
                    messageData.NetworkOperation =
                        $@"{{
                                instance.{message.MemberName} = new {GetMemberType(message.MemberType)}();
                                for (int i = 0; i < data.Value.Count; i++)
                                {{
                                    instance.{message.MemberName}.Add(value);
                                }}
                            }}";
                }
            }
            else if ((message.Type & DynamicMessageType.Queue) == DynamicMessageType.Queue)
            {
                if (message.Action == DynamicMessageAction.Set)
                {
                    subscriptionTemplate = "Handlers.SubscribeCustomSetValue";
                    messageData.MemberType = GetMemberType(message.MemberType);

                    if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                    {
                        messageData.NetworkMemberType = "List<string>";
                        messageData.Operation =
                        $@"{{ 
                                var valueIds = new List<string>();
                                for (int i = 0; i < data.Value.Count; i++)
                                {{
                                    if (!TryGetId(data.Value.ElementAt(i), out string valueId)) return;
                                    valueIds.Add(valueId);
                                }}
                                network.SendAll(new {"Network_" + message.MessageName}(instanceId, valueIds));
                            }}";
                        messageData.NetworkOperation =
                        $@"{{
                            instance.{message.MemberName} = new {GetMemberType(message.MemberType)}();
                            for (int i = 0; i < data.Value.Count; i++)
                            {{
                                if (!objectManager.TryGetObject(data.Value[i], out {GetGenericType(message.MemberType)} value)) return;
                                instance.{message.MemberName}.Enqueue(value);
                            }}
                        }}";
                    }
                    else
                    {
                        messageData.NetworkMemberType = $"List<{GetGenericType(message.MemberType)}>";
                        messageData.Operation =
                        $@"{{
                            network.SendAll(new {"Network_" + message.MessageName}(instanceId, data.Value.ToList()));
                        }}";
                        messageData.NetworkOperation =
                        $@"{{
                            instance.{message.MemberName} = new {GetMemberType(message.MemberType)}();
                            for (int i = 0; i < data.Value.Count; i++)
                            {{
                                instance.{message.MemberName}.Add(value);
                            }}
                        }}";
                    }
                }
                else if (message.Action == DynamicMessageAction.CollectionAdd)
                {
                    if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                    {
                        subscriptionTemplate = "Handlers.SubscribeCustomNetworkSetReference";
                        messageData.Operation = $"instance.{message.MemberName}.Enqueue(value)";
                    }
                    else
                    {
                        subscriptionTemplate = "Handlers.SubscribeCustomNetworkSetValue";
                        messageData.Operation = $"instance.{message.MemberName}.Enqueue(data.Value)";
                    }
                }
                else if (message.Action == DynamicMessageAction.CollectionRemove)
                {
                    if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                        subscriptionTemplate = "Handlers.SubscribeCustomNetworkSetReference";
                    else
                        subscriptionTemplate = "Handlers.SubscribeCustomNetworkSetValue";

                    messageData.Operation = $"instance.{message.MemberName}.Dequeue()";
                }
                else
                    throw new InvalidOperationException("Unsupported Message Subscription for Queue");
            }
            else if ((message.Type & DynamicMessageType.Array) == DynamicMessageType.Array)
            {
                if (message.Action == DynamicMessageAction.ArraySet)
                {
                    subscriptionTemplate = "Handlers.SubscribeCustomSetValue";

                    if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                    {
                        messageData.NetworkMemberType = "List<(int Index, string Id)>";
                        messageData.Operation =
                        $@"{{ 
                            var valueIds = new List<(int Index, string Id)>();
                            for (int i = 0; i < data.Value.Length; i++)
                            {{
                                if(data.Value[i] == null) continue;
                                if (!TryGetId(data.Value[i], out string valueId)) return;
                                valueIds.Add(new (i,valueId));
                            }}
                            network.SendAll(new {"Network_" + message.MessageName}(instanceId, valueIds, data.Value.Length));
                        }}";
                        messageData.NetworkOperation =
                        $@"{{
                            instance.{message.MemberName} = new {message.MemberType.GetElementType().Name}[data.Length];
                            for (int i = 0; i < data.Value.Count; i++)
                            {{
                                if (!objectManager.TryGetObject(data.Value[i].Id, out {message.MemberType.GetElementType().Name} value)) return;
                                instance.{message.MemberName}[data.Value[i].Index] = value;
                            }}
                        }}";
                    }
                    else
                    {
                        messageData.NetworkMemberType = "List<(int Index, string Id)>";
                        messageData.Operation =
                        $@"{{ 
                            var valueIds = new List<(int Index, string Id)>();
                            for (int i = 0; i < data.Value.Length; i++)
                            {{
                                if(data.Value[i] == null) continue;
                                valueIds.Add(new (i, data.Value[i]));
                            }}
                            network.SendAll(new {"Network_" + message.MessageName}(instanceId, valueIds, data.Value.Length));
                        }}";
                        messageData.NetworkOperation =
                        $@"{{
                            instance.{message.MemberName} = new {message.MemberType.GetElementType().Name}[data.Length];
                            for (int i = 0; i < data.Value.Count; i++)
                            {{
                                instance.{message.MemberName}[data.Value[i].Index] = data.Value[i];
                            }}
                        }}";
                    }
                }
                else if (message.Action == DynamicMessageAction.ArrayChange)
                {
                    if ((message.Type & DynamicMessageType.ObjectManagerType) == DynamicMessageType.ObjectManagerType)
                    {
                        subscriptionTemplate = "Handlers.SubscribeCustomSetValue";
                        messageData.Operation = $"{{if (!TryGetId(data.Value, out string valueId) && data.Value != null) return;network.SendAll(new {"Network_" + message.MessageName}(instanceId, valueId, data.Index));}}";
                        messageData.NetworkOperation = $"{{if (!objectManager.TryGetObject(data.ValueId, out {message.MemberType.Name} value) && data.ValueId != null) return; instance.{message.MemberName}[data.Index] = value;}}";
                    }
                    else
                    {
                        subscriptionTemplate = "Handlers.SubscribeCustomSetValue";
                        messageData.Operation = $"network.SendAll(new {"Network_" + message.MessageName}(instanceId, data.Value, data.Index));";
                        messageData.NetworkOperation = $"instance.{message.MemberName}[data.Index] = data.Value";
                    }

                    messageData.NetworkMemberType = message.MemberType.Name;
                }
                else
                    throw new InvalidOperationException("Unsupported Message Subscription for Array");
            }
            else
                throw new InvalidOperationException("Unsupported Message Subscription");

            return TemplateParser.Parse(subscriptionTemplate, messageData);
        }
        #endregion
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
    }
}
