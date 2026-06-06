using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Library;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPatchBuilder
    {
        private readonly IObjectManager objectManager;
        private readonly DynamicSyncPropertyBuilder dynamicSyncPropertyBuilder;
        private readonly DynamicSyncFieldBuilder dynamicSyncFieldBuilder;
        private readonly DynamicSyncPropertyArrayBuilder dynamicSyncPropertyArrayBuilder;
        private readonly DynamicSyncFieldArrayBuilder dynamicSyncFieldArrayBuilder;
        private readonly DynamicSyncPropertyMBListBuilder dynamicSyncPropertyMBListBuilder;
        private readonly DynamicSyncFieldMBListBuilder dynamicSyncFieldMBListBuilder;
        private readonly DynamicSyncFieldListBuilder dynamicSyncFieldListBuilder;
        private readonly DynamicSyncPropertyQueueBuilder dynamicSyncPropertyQueueBuilder;
        private readonly DynamicSyncFieldQueueBuilder dynamicSyncFieldQueueBuilder;
        private readonly DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder;
        private readonly DynamicSyncPropertyListBuilder dynamicSyncPropertyListBuilder;
        private readonly DynamicSyncFieldPropertyOwnerBuilder dynamicSyncFieldPropertyOwnerBuilder;

        public DynamicSyncPatchBuilder(IObjectManager objectManager,
            DynamicSyncPropertyBuilder dynamicSyncPropertyBuilder,
            DynamicSyncFieldBuilder dynamicSyncFieldBuilder,
            DynamicSyncPropertyArrayBuilder dynamicSyncPropertyArrayBuilder,
            DynamicSyncFieldArrayBuilder dynamicSyncFieldArrayBuilder,
            DynamicSyncPropertyMBListBuilder dynamicSyncPropertyMBListBuilder,
            DynamicSyncFieldMBListBuilder dynamicSyncFieldMBListBuilder,
            DynamicSyncPropertyListBuilder dynamicSyncPropertyListBuilder,
            DynamicSyncFieldListBuilder dynamicSyncFieldListBuilder,
            DynamicSyncPropertyQueueBuilder dynamicSyncPropertyQueueBuilder,
            DynamicSyncFieldQueueBuilder dynamicSyncFieldQueueBuilder,
            DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder,
            DynamicSyncFieldPropertyOwnerBuilder dynamicSyncFieldPropertyOwnerBuilder
            )
        {
            this.objectManager = objectManager;
            this.dynamicSyncPropertyBuilder = dynamicSyncPropertyBuilder;
            this.dynamicSyncFieldBuilder = dynamicSyncFieldBuilder;
            this.dynamicSyncPropertyArrayBuilder = dynamicSyncPropertyArrayBuilder;
            this.dynamicSyncFieldArrayBuilder = dynamicSyncFieldArrayBuilder;
            this.dynamicSyncPropertyMBListBuilder = dynamicSyncPropertyMBListBuilder;
            this.dynamicSyncFieldMBListBuilder = dynamicSyncFieldMBListBuilder;
            this.dynamicSyncPropertyListBuilder = dynamicSyncPropertyListBuilder;
            this.dynamicSyncFieldListBuilder = dynamicSyncFieldListBuilder;
            this.dynamicSyncPropertyQueueBuilder = dynamicSyncPropertyQueueBuilder;
            this.dynamicSyncFieldQueueBuilder = dynamicSyncFieldQueueBuilder;
            this.dynamicSyncConstantsBuilder = dynamicSyncConstantsBuilder;
            this.dynamicSyncFieldPropertyOwnerBuilder = dynamicSyncFieldPropertyOwnerBuilder;
        }
        public List<SyntaxTree> Build(Type declaringType, DynamicSyncRegistryItem dynamicRegistryItem)
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
                usings.Add(DynamicSyncUtils.GetNamespace(propertyInfo.PropertyType));

                if (!propertyInfo.PropertyType.IsGenericType && !propertyInfo.PropertyType.IsArray)
                {
                    prefixes.Add(dynamicSyncPropertyBuilder.GetPrefix(propertyItem));
                    messages.AddRange(dynamicSyncPropertyBuilder.GetMessages(propertyItem));
                    messageHandlers.Add(dynamicSyncPropertyBuilder.GetSubscription(propertyItem));
                }
                else if(propertyInfo.PropertyType.IsArray)
                {
                    prefixes.Add(dynamicSyncPropertyArrayBuilder.GetPrefix(propertyItem));
                    transpilers.Add(dynamicSyncPropertyArrayBuilder.GetTranspiler(propertyItem));
                    messages.AddRange(dynamicSyncPropertyArrayBuilder.GetMessages(propertyItem));
                    messageHandlers.Add(dynamicSyncPropertyArrayBuilder.GetSubscription(propertyItem));
                }
                else if (propertyInfo.PropertyType.Name.Contains("MBList"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(propertyInfo.PropertyType.GetGenericArguments()[0]));
                    prefixes.Add(dynamicSyncPropertyMBListBuilder.GetPrefix(propertyItem));
                    transpilers.Add(dynamicSyncPropertyMBListBuilder.GetTranspiler(propertyItem));
                    messages.AddRange(dynamicSyncPropertyMBListBuilder.GetMessages(propertyItem));
                    messageHandlers.Add(dynamicSyncPropertyMBListBuilder.GetSubscription(propertyItem));
                }
                else if (propertyInfo.PropertyType.Name.Contains("List"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(propertyInfo.PropertyType.GetGenericArguments()[0]));
                    prefixes.Add(dynamicSyncPropertyListBuilder.GetPrefix(propertyItem));
                    transpilers.Add(dynamicSyncPropertyListBuilder.GetTranspiler(propertyItem));
                    messages.AddRange(dynamicSyncPropertyListBuilder.GetMessages(propertyItem));
                    messageHandlers.Add(dynamicSyncPropertyListBuilder.GetSubscription(propertyItem));
                }
                else if (propertyInfo.PropertyType.Name.Contains("Queue"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(propertyInfo.PropertyType.GetGenericArguments()[0]));
                    prefixes.Add(dynamicSyncPropertyQueueBuilder.GetPrefix(propertyItem));
                    transpilers.Add(dynamicSyncPropertyQueueBuilder.GetTranspiler(propertyItem));
                    messages.AddRange(dynamicSyncPropertyQueueBuilder.GetMessages(propertyItem));
                    messageHandlers.Add(dynamicSyncPropertyQueueBuilder.GetSubscription(propertyItem));
                }
            }

            foreach (var fieldItem in dynamicRegistryItem.Fields)
            {
                var fieldInfo = fieldItem.Value;

                ValidateType(fieldInfo.FieldType);
                usings.Add(DynamicSyncUtils.GetNamespace(fieldInfo.FieldType));
                if (!fieldInfo.FieldType.IsGenericType && !fieldInfo.FieldType.IsArray)
                {
                    transpilers.Add(dynamicSyncFieldBuilder.GetTranspiler(fieldItem));
                    messages.AddRange(dynamicSyncFieldBuilder.GetMessages(fieldItem));
                    messageHandlers.Add(dynamicSyncFieldBuilder.GetSubscription(fieldItem));
                }
                else if (fieldInfo.FieldType.IsArray)
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(fieldInfo.FieldType.GetElementType()));
                    transpilers.Add(dynamicSyncFieldArrayBuilder.GetTranspiler(fieldItem));
                    messages.AddRange(dynamicSyncFieldArrayBuilder.GetMessages(fieldItem));
                    messageHandlers.Add(dynamicSyncFieldArrayBuilder.GetSubscription(fieldItem));
                }
                else if (fieldInfo.FieldType.Name.Contains("MBList"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    transpilers.Add(dynamicSyncFieldMBListBuilder.GetTranspiler(fieldItem));
                    messages.AddRange(dynamicSyncFieldMBListBuilder.GetMessages(fieldItem));
                    messageHandlers.Add(dynamicSyncFieldMBListBuilder.GetSubscription(fieldItem));
                }
                else if (fieldInfo.FieldType.Name.Contains("List"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    transpilers.Add(dynamicSyncFieldListBuilder.GetTranspiler(fieldItem));
                    messages.AddRange(dynamicSyncFieldListBuilder.GetMessages(fieldItem));
                    messageHandlers.Add(dynamicSyncFieldListBuilder.GetSubscription(fieldItem));
                }
                else if (fieldInfo.FieldType.Name.Contains("Queue"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    transpilers.Add(dynamicSyncFieldQueueBuilder.GetTranspiler(fieldItem));
                    messages.AddRange(dynamicSyncFieldQueueBuilder.GetMessages(fieldItem));
                    messageHandlers.Add(dynamicSyncFieldQueueBuilder.GetSubscription(fieldItem));
                }
                else if (fieldInfo.FieldType.Name.Contains("PropertyOwner"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    transpilers.Add(dynamicSyncFieldPropertyOwnerBuilder.GetTranspiler(fieldItem));
                    messages.AddRange(dynamicSyncFieldPropertyOwnerBuilder.GetMessages(fieldItem));
                    messageHandlers.Add(dynamicSyncFieldPropertyOwnerBuilder.GetSubscription(fieldItem));
                }
            }

            var patchTemplate = TemplateParser.Parse("Patches.DynamicPatchTemplate", new
            {
                Libraries = usings.Distinct(),
                DeclaringType = declaringType.Name,
                TargetMethods = dynamicRegistryItem.TargetMethods,
                Prefixes = prefixes,
                Transpilers = transpilers,
            });

            DynamicSyncConfiguration.ExportFile($"{declaringType.Name}/{declaringType.Name}_DynamicPatches.cs", patchTemplate);

            var handlerTemplate = TemplateParser.Parse("Handlers.DynamicHandlerTemplate", new
            {
                Libraries = usings.Distinct(),
                DeclaringType = declaringType.Name,
                Subscriptions = messageHandlers
            });

            DynamicSyncConfiguration.ExportFile($"{declaringType.Name}/{declaringType.Name}_Handler.cs", handlerTemplate);

            syntaxTrees.Add(CSharpSyntaxTree.ParseText(patchTemplate));
            syntaxTrees.AddRange(messages.Select(m => CSharpSyntaxTree.ParseText(m)));
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(handlerTemplate));
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
        }
    }
}
