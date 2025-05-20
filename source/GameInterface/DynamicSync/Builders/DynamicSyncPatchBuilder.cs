using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPatchBuilder
    {
        private readonly IObjectManager objectManager;
        private readonly DynamicSyncPropertyBuilder dynamicSyncPropertyBuilder;
        private readonly DynamicSyncFieldBuilder dynamicSyncFieldBuilder;

        public DynamicSyncPatchBuilder(IObjectManager objectManager, DynamicSyncPropertyBuilder dynamicSyncPropertyBuilder, DynamicSyncFieldBuilder dynamicSyncFieldBuilder)
        {
            this.objectManager = objectManager;
            this.dynamicSyncPropertyBuilder = dynamicSyncPropertyBuilder;
            this.dynamicSyncFieldBuilder = dynamicSyncFieldBuilder;
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
                usings.Add(propertyInfo.PropertyType.Namespace);
                prefixes.Add(dynamicSyncPropertyBuilder.GetPrefix(propertyInfo));
                messages.AddRange(dynamicSyncPropertyBuilder.GetMessages(propertyInfo));
                messageHandlers.Add(dynamicSyncPropertyBuilder.GetSubscription(propertyInfo));
            }

            foreach (var fieldInfo in dynamicRegistryItem.Fields)
            {
                ValidateType(fieldInfo.FieldType);
                usings.Add(fieldInfo.FieldType.Namespace);
                transpilers.Add(dynamicSyncFieldBuilder.GetTranspiler(fieldInfo));
                messages.AddRange(dynamicSyncFieldBuilder.GetMessages(fieldInfo));
                messageHandlers.Add(dynamicSyncFieldBuilder.GetSubscription(fieldInfo));
            }

            var patchTemplate = TemplateParser.Parse("Patches.DynamicPatchTemplate", new
            {
                Libraries = usings.Distinct(),
                DeclaringType = declaringType.Name,
                TargetMethods = dynamicRegistryItem.TargetMethods,
                Prefixes = prefixes,
                Transpilers = transpilers
            });

            DynamicSyncConfiguration.ExportFile($"{declaringType.Name}/{declaringType.Name}_DynamicPatch.cs", patchTemplate);

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
            // Prevent unsupported types
            if (!objectManager.IsTypeManaged(typeToVerify) && !RuntimeTypeModel.Default.CanSerialize(typeToVerify))
            {
                throw new NotSupportedException(
                    $"{typeToVerify.Name} is not serializable and not managed by the object manager. " +
                    $"Either manage the type using the object manager or make this type serializable");
            }
        }
    }
}
