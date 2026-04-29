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
            DynamicSyncConstantsBuilder dynamicSyncConstantsBuilder
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

            foreach(var propertyInfo in dynamicRegistryItem.Properties)
            {
                ValidateType(propertyInfo.PropertyType);
                usings.Add(DynamicSyncUtils.GetNamespace(propertyInfo.PropertyType));

                if (!propertyInfo.PropertyType.IsGenericType && !propertyInfo.PropertyType.IsArray)
                {
                    prefixes.Add(dynamicSyncPropertyBuilder.GetPrefix(propertyInfo));
                    messages.AddRange(dynamicSyncPropertyBuilder.GetMessages(propertyInfo));
                    messageHandlers.Add(dynamicSyncPropertyBuilder.GetSubscription(propertyInfo));
                }
                else if(propertyInfo.PropertyType.IsArray)
                {
                    prefixes.Add(dynamicSyncPropertyArrayBuilder.GetPrefix(propertyInfo));
                    transpilers.Add(dynamicSyncPropertyArrayBuilder.GetTranspiler(propertyInfo));
                    messages.AddRange(dynamicSyncPropertyArrayBuilder.GetMessages(propertyInfo));
                    messageHandlers.Add(dynamicSyncPropertyArrayBuilder.GetSubscription(propertyInfo));
                }
                else if (propertyInfo.PropertyType.Name.Contains("MBList"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(propertyInfo.PropertyType.GetGenericArguments()[0]));
                    prefixes.Add(dynamicSyncPropertyMBListBuilder.GetPrefix(propertyInfo));
                    transpilers.Add(dynamicSyncPropertyMBListBuilder.GetTranspiler(propertyInfo));
                    messages.AddRange(dynamicSyncPropertyMBListBuilder.GetMessages(propertyInfo));
                    messageHandlers.Add(dynamicSyncPropertyMBListBuilder.GetSubscription(propertyInfo));
                }
                else if (propertyInfo.PropertyType.Name.Contains("List"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(propertyInfo.PropertyType.GetGenericArguments()[0]));
                    prefixes.Add(dynamicSyncPropertyListBuilder.GetPrefix(propertyInfo));
                    transpilers.Add(dynamicSyncPropertyListBuilder.GetTranspiler(propertyInfo));
                    messages.AddRange(dynamicSyncPropertyListBuilder.GetMessages(propertyInfo));
                    messageHandlers.Add(dynamicSyncPropertyListBuilder.GetSubscription(propertyInfo));
                }
                else if (propertyInfo.PropertyType.Name.Contains("Queue"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(propertyInfo.PropertyType.GetGenericArguments()[0]));
                    prefixes.Add(dynamicSyncPropertyQueueBuilder.GetPrefix(propertyInfo));
                    transpilers.Add(dynamicSyncPropertyQueueBuilder.GetTranspiler(propertyInfo));
                    messages.AddRange(dynamicSyncPropertyQueueBuilder.GetMessages(propertyInfo));
                    messageHandlers.Add(dynamicSyncPropertyQueueBuilder.GetSubscription(propertyInfo));
                }
            }

            foreach (var fieldInfo in dynamicRegistryItem.Fields)
            {
                ValidateType(fieldInfo.FieldType);
                usings.Add(DynamicSyncUtils.GetNamespace(fieldInfo.FieldType));
                if (!fieldInfo.FieldType.IsGenericType && !fieldInfo.FieldType.IsArray)
                {
                    transpilers.Add(dynamicSyncFieldBuilder.GetTranspiler(fieldInfo));
                    messages.AddRange(dynamicSyncFieldBuilder.GetMessages(fieldInfo));
                    messageHandlers.Add(dynamicSyncFieldBuilder.GetSubscription(fieldInfo));
                }
                else if (fieldInfo.FieldType.IsArray)
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(fieldInfo.FieldType.GetElementType()));
                    transpilers.Add(dynamicSyncFieldArrayBuilder.GetTranspiler(fieldInfo));
                    messages.AddRange(dynamicSyncFieldArrayBuilder.GetMessages(fieldInfo));
                    messageHandlers.Add(dynamicSyncFieldArrayBuilder.GetSubscription(fieldInfo));
                }
                else if (fieldInfo.FieldType.Name.Contains("MBList"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    transpilers.Add(dynamicSyncFieldMBListBuilder.GetTranspiler(fieldInfo));
                    messages.AddRange(dynamicSyncFieldMBListBuilder.GetMessages(fieldInfo));
                    messageHandlers.Add(dynamicSyncFieldMBListBuilder.GetSubscription(fieldInfo));
                }
                else if (fieldInfo.FieldType.Name.Contains("List"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    transpilers.Add(dynamicSyncFieldListBuilder.GetTranspiler(fieldInfo));
                    messages.AddRange(dynamicSyncFieldListBuilder.GetMessages(fieldInfo));
                    messageHandlers.Add(dynamicSyncFieldListBuilder.GetSubscription(fieldInfo));
                }
                else if (fieldInfo.FieldType.Name.Contains("Queue"))
                {
                    usings.Add(DynamicSyncUtils.GetNamespace(fieldInfo.FieldType.GetGenericArguments()[0]));
                    transpilers.Add(dynamicSyncFieldQueueBuilder.GetTranspiler(fieldInfo));
                    messages.AddRange(dynamicSyncFieldQueueBuilder.GetMessages(fieldInfo));
                    messageHandlers.Add(dynamicSyncFieldQueueBuilder.GetSubscription(fieldInfo));
                }
            }

            var patchTemplate = TemplateParser.Parse("Patches.DynamicPatchTemplate", new
            {
                Libraries = usings.Distinct(),
                DeclaringType = declaringType.Name,
                TargetMethods = dynamicRegistryItem.TargetMethods,
                Prefixes = prefixes,
                Transpilers = transpilers
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
