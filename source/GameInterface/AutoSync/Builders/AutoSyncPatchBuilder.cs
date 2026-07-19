using GameInterface.AutoSync.Templates;
using GameInterface.Services.ObjectManager;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.AutoSync.Builders
{
    public class AutoSyncPatchBuilder
    {
        private readonly IObjectManager objectManager;
        private readonly AutoSyncPropertyBuilder autoSyncPropertyBuilder;
        private readonly AutoSyncFieldBuilder autoSyncFieldBuilder;
        private readonly AutoSyncPropertyArrayBuilder autoSyncPropertyArrayBuilder;
        private readonly AutoSyncFieldArrayBuilder autoSyncFieldArrayBuilder;
        private readonly AutoSyncPropertyMBListBuilder autoSyncPropertyMBListBuilder;
        private readonly AutoSyncFieldMBListBuilder autoSyncFieldMBListBuilder;
        private readonly AutoSyncFieldListBuilder autoSyncFieldListBuilder;
        private readonly AutoSyncPropertyQueueBuilder autoSyncPropertyQueueBuilder;
        private readonly AutoSyncFieldQueueBuilder autoSyncFieldQueueBuilder;
        private readonly AutoSyncPropertyDictionaryBuilder autoSyncPropertyDictionaryBuilder;
        private readonly AutoSyncFieldDictionaryBuilder autoSyncFieldDictionaryBuilder;
        private readonly AutoSyncConstantsBuilder autoSyncConstantsBuilder;
        private readonly AutoSyncPropertyListBuilder autoSyncPropertyListBuilder;
        private readonly AutoSyncFieldPropertyOwnerBuilder autoSyncFieldPropertyOwnerBuilder;

        public AutoSyncPatchBuilder(IObjectManager objectManager,
            AutoSyncPropertyBuilder autoSyncPropertyBuilder,
            AutoSyncFieldBuilder autoSyncFieldBuilder,
            AutoSyncPropertyArrayBuilder autoSyncPropertyArrayBuilder,
            AutoSyncFieldArrayBuilder autoSyncFieldArrayBuilder,
            AutoSyncPropertyMBListBuilder autoSyncPropertyMBListBuilder,
            AutoSyncFieldMBListBuilder autoSyncFieldMBListBuilder,
            AutoSyncPropertyListBuilder autoSyncPropertyListBuilder,
            AutoSyncFieldListBuilder autoSyncFieldListBuilder,
            AutoSyncPropertyQueueBuilder autoSyncPropertyQueueBuilder,
            AutoSyncFieldQueueBuilder autoSyncFieldQueueBuilder,
            AutoSyncPropertyDictionaryBuilder autoSyncPropertyDictionaryBuilder,
            AutoSyncFieldDictionaryBuilder autoSyncFieldDictionaryBuilder,
            AutoSyncConstantsBuilder autoSyncConstantsBuilder,
            AutoSyncFieldPropertyOwnerBuilder autoSyncFieldPropertyOwnerBuilder
            )
        {
            this.objectManager = objectManager;
            this.autoSyncPropertyBuilder = autoSyncPropertyBuilder;
            this.autoSyncFieldBuilder = autoSyncFieldBuilder;
            this.autoSyncPropertyArrayBuilder = autoSyncPropertyArrayBuilder;
            this.autoSyncFieldArrayBuilder = autoSyncFieldArrayBuilder;
            this.autoSyncPropertyMBListBuilder = autoSyncPropertyMBListBuilder;
            this.autoSyncFieldMBListBuilder = autoSyncFieldMBListBuilder;
            this.autoSyncPropertyListBuilder = autoSyncPropertyListBuilder;
            this.autoSyncFieldListBuilder = autoSyncFieldListBuilder;
            this.autoSyncPropertyQueueBuilder = autoSyncPropertyQueueBuilder;
            this.autoSyncFieldQueueBuilder = autoSyncFieldQueueBuilder;
            this.autoSyncPropertyDictionaryBuilder = autoSyncPropertyDictionaryBuilder;
            this.autoSyncFieldDictionaryBuilder = autoSyncFieldDictionaryBuilder;
            this.autoSyncConstantsBuilder = autoSyncConstantsBuilder;
            this.autoSyncFieldPropertyOwnerBuilder = autoSyncFieldPropertyOwnerBuilder;
        }
        public List<SyntaxTree> Build(Type declaringType, AutoSyncRegistryItem dynamicRegistryItem)
        {
            List<string> prefixes = new List<string>();
            List<string> transpilers = new List<string>();
            List<string> messages = new List<string>();
            List<string> messageHandlers = new List<string>();
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

            List<string> usings = new List<string>
            {
                declaringType.Namespace
            };

            foreach(var propertyItem in dynamicRegistryItem.Properties)
            {
                var propertyInfo = propertyItem.Value;

                ValidateType(propertyInfo.PropertyType);
                usings.Add(AutoSyncUtils.GetNamespace(propertyInfo.PropertyType));

                if (!propertyInfo.PropertyType.IsGenericType && !propertyInfo.PropertyType.IsArray)
                {
                    prefixes.Add(autoSyncPropertyBuilder.GetPrefix(propertyItem));
                    messages.AddRange(autoSyncPropertyBuilder.GetMessages(propertyItem));
                    messageHandlers.Add(autoSyncPropertyBuilder.GetSubscription(propertyItem));
                }
                else if(propertyInfo.PropertyType.IsArray)
                {
                    prefixes.Add(autoSyncPropertyArrayBuilder.GetPrefix(propertyItem));
                    transpilers.Add(autoSyncPropertyArrayBuilder.GetTranspiler(propertyItem));
                    messages.AddRange(autoSyncPropertyArrayBuilder.GetMessages(propertyItem));
                    messageHandlers.Add(autoSyncPropertyArrayBuilder.GetSubscription(propertyItem));
                }
                else if (IsGenericTypeOf(propertyInfo.PropertyType, typeof(MBList<>)))
                {
                    usings.Add(AutoSyncUtils.GetNamespace(propertyInfo.PropertyType.GetGenericArguments()[0]));
                    prefixes.Add(autoSyncPropertyMBListBuilder.GetPrefix(propertyItem));
                    transpilers.Add(autoSyncPropertyMBListBuilder.GetTranspiler(propertyItem));
                    messages.AddRange(autoSyncPropertyMBListBuilder.GetMessages(propertyItem));
                    messageHandlers.Add(autoSyncPropertyMBListBuilder.GetSubscription(propertyItem));
                }
                else if (IsGenericTypeOf(propertyInfo.PropertyType, typeof(List<>)))
                {
                    usings.Add(AutoSyncUtils.GetNamespace(propertyInfo.PropertyType.GetGenericArguments()[0]));
                    prefixes.Add(autoSyncPropertyListBuilder.GetPrefix(propertyItem));
                    transpilers.Add(autoSyncPropertyListBuilder.GetTranspiler(propertyItem));
                    messages.AddRange(autoSyncPropertyListBuilder.GetMessages(propertyItem));
                    messageHandlers.Add(autoSyncPropertyListBuilder.GetSubscription(propertyItem));
                }
                else if (IsGenericTypeOf(propertyInfo.PropertyType, typeof(Queue<>)))
                {
                    usings.Add(AutoSyncUtils.GetNamespace(propertyInfo.PropertyType.GetGenericArguments()[0]));
                    prefixes.Add(autoSyncPropertyQueueBuilder.GetPrefix(propertyItem));
                    transpilers.AddRange(autoSyncPropertyQueueBuilder.GetTranspilers(propertyItem));
                    messages.AddRange(autoSyncPropertyQueueBuilder.GetMessages(propertyItem));
                    messageHandlers.AddRange(autoSyncPropertyQueueBuilder.GetSubscriptions(propertyItem));
                }
                else if (IsGenericTypeOf(propertyInfo.PropertyType, typeof(Dictionary<,>)))
                {
                    usings.Add(AutoSyncUtils.GetNamespace(propertyInfo.PropertyType.GetGenericArguments()[0]));
                    usings.Add(AutoSyncUtils.GetNamespace(propertyInfo.PropertyType.GetGenericArguments()[1]));
                    prefixes.Add(autoSyncPropertyDictionaryBuilder.GetPrefix(propertyItem));
                    transpilers.AddRange(autoSyncPropertyDictionaryBuilder.GetTranspilers(propertyItem));
                    messages.AddRange(autoSyncPropertyDictionaryBuilder.GetMessages(propertyItem));
                    messageHandlers.AddRange(autoSyncPropertyDictionaryBuilder.GetSubscriptions(propertyItem));
                }
            }

            foreach (var fieldItem in dynamicRegistryItem.Fields)
            {
                var fieldInfo = fieldItem.Value;

                ValidateType(fieldInfo.FieldType);
                usings.Add(AutoSyncUtils.GetNamespace(fieldInfo.FieldType));
                if (!fieldInfo.FieldType.IsGenericType && !fieldInfo.FieldType.IsArray)
                {
                    transpilers.Add(autoSyncFieldBuilder.GetTranspiler(fieldItem));
                    messages.AddRange(autoSyncFieldBuilder.GetMessages(fieldItem));
                    messageHandlers.Add(autoSyncFieldBuilder.GetSubscription(fieldItem));
                }
                else if (fieldInfo.FieldType.IsArray)
                {
                    usings.Add(AutoSyncUtils.GetNamespace(fieldInfo.FieldType.GetElementType()));
                    transpilers.Add(autoSyncFieldArrayBuilder.GetTranspiler(fieldItem));
                    messages.AddRange(autoSyncFieldArrayBuilder.GetMessages(fieldItem));
                    messageHandlers.Add(autoSyncFieldArrayBuilder.GetSubscription(fieldItem));
                }
                else if (IsGenericTypeOf(fieldInfo.FieldType, typeof(MBList<>)))
                {
                    usings.Add(AutoSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    transpilers.Add(autoSyncFieldMBListBuilder.GetTranspiler(fieldItem));
                    messages.AddRange(autoSyncFieldMBListBuilder.GetMessages(fieldItem));
                    messageHandlers.Add(autoSyncFieldMBListBuilder.GetSubscription(fieldItem));
                }
                else if (IsGenericTypeOf(fieldInfo.FieldType, typeof(List<>)))
                {
                    usings.Add(AutoSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    transpilers.Add(autoSyncFieldListBuilder.GetTranspiler(fieldItem));
                    messages.AddRange(autoSyncFieldListBuilder.GetMessages(fieldItem));
                    messageHandlers.Add(autoSyncFieldListBuilder.GetSubscription(fieldItem));
                }
                else if (IsGenericTypeOf(fieldInfo.FieldType, typeof(Queue<>)))
                {
                    usings.Add(AutoSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    transpilers.AddRange(autoSyncFieldQueueBuilder.GetTranspilers(fieldItem));
                    messages.AddRange(autoSyncFieldQueueBuilder.GetMessages(fieldItem));
                    messageHandlers.AddRange(autoSyncFieldQueueBuilder.GetSubscriptions(fieldItem));
                }
                else if (IsGenericTypeOf(fieldInfo.FieldType, typeof(Dictionary<,>)))
                {
                    usings.Add(AutoSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    usings.Add(AutoSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[1]));
                    transpilers.AddRange(autoSyncFieldDictionaryBuilder.GetTranspilers(fieldItem));
                    messages.AddRange(autoSyncFieldDictionaryBuilder.GetMessages(fieldItem));
                    messageHandlers.AddRange(autoSyncFieldDictionaryBuilder.GetSubscriptions(fieldItem));
                }
                else if (IsGenericTypeOf(fieldInfo.FieldType, typeof(PropertyOwner<>)))
                {
                    usings.Add(AutoSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    transpilers.Add(autoSyncFieldPropertyOwnerBuilder.GetTranspiler(fieldItem));
                    messages.AddRange(autoSyncFieldPropertyOwnerBuilder.GetMessages(fieldItem));
                    messageHandlers.Add(autoSyncFieldPropertyOwnerBuilder.GetSubscription(fieldItem));
                }
            }

            var declaringTypeName = AutoSyncUtils.GetSimpleTypeName(declaringType).Replace(".", "_");
            var patchTemplate = TemplateParser.Parse("Patches.DynamicPatchTemplate", new
            {
                Libraries = usings.Distinct(),
                DeclaringType = AutoSyncUtils.GetSimpleTypeName(declaringType),
                PatchClassName = $"{declaringTypeName}_DynamicPatches",
                PatchCategory = (string)null,
                IncludeDeclaredMethods = true,
                TargetMethods = dynamicRegistryItem.TargetMethods,
                Prefixes = prefixes,
                Transpilers = transpilers,
            });

            AutoSyncConfiguration.ExportFile($"{declaringType.Name}/{declaringType.Name}_DynamicPatches.cs", patchTemplate);

            var handlerTemplate = TemplateParser.Parse("Handlers.DynamicHandlerTemplate", new
            {
                Libraries = usings.Distinct(),
                DeclaringType = AutoSyncUtils.GetSimpleTypeName(declaringType),
                DeclaringTypeName = declaringType.Name,
                Subscriptions = messageHandlers
            });

            AutoSyncConfiguration.ExportFile($"{declaringType.Name}/{declaringType.Name}_Handler.cs", handlerTemplate);

            syntaxTrees.Add(CSharpSyntaxTree.ParseText(patchTemplate));
            foreach (var categorizedTargets in dynamicRegistryItem.CategorizedTargetMethods)
            {
                var categoryPatchTemplate = TemplateParser.Parse("Patches.DynamicPatchTemplate", new
                {
                    Libraries = usings.Distinct(),
                    DeclaringType = AutoSyncUtils.GetSimpleTypeName(declaringType),
                    PatchClassName = $"{declaringTypeName}_{categorizedTargets.Key}_DynamicPatches",
                    PatchCategory = categorizedTargets.Key,
                    IncludeDeclaredMethods = false,
                    TargetMethods = categorizedTargets.Value,
                    Prefixes = Enumerable.Empty<string>(),
                    Transpilers = transpilers,
                });

                AutoSyncConfiguration.ExportFile($"{declaringType.Name}/{declaringType.Name}_{categorizedTargets.Key}_DynamicPatches.cs", categoryPatchTemplate);
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(categoryPatchTemplate));
            }
            syntaxTrees.AddRange(messages.Select(m => CSharpSyntaxTree.ParseText(m)));
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(handlerTemplate));
            return syntaxTrees;
        }


        // Exact generic-definition match: substring checks on Type.Name ("List", "Queue", ...) are
        // order-sensitive (MBList before List) and can false-match unrelated types
        private static bool IsGenericTypeOf(Type type, Type genericTypeDefinition)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition;
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
        }
    }
}
