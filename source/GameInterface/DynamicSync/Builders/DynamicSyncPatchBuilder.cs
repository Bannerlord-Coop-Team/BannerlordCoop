using GameInterface.DynamicSync.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPatchBuilder
    {
        private readonly DynamicSyncPropertyBuilder dynamicSyncPropertyBuilder;
        private readonly DynamicSyncFieldBuilder dynamicSyncFieldBuilder;
        private readonly DynamicSyncPropertyArrayBuilder dynamicSyncPropertyArrayBuilder;
        private readonly DynamicSyncFieldArrayBuilder dynamicSyncFieldArrayBuilder;

        public DynamicSyncPatchBuilder(DynamicSyncPropertyBuilder dynamicSyncPropertyBuilder,
            DynamicSyncFieldBuilder dynamicSyncFieldBuilder,
            DynamicSyncPropertyArrayBuilder dynamicSyncPropertyArrayBuilder,
            DynamicSyncFieldArrayBuilder dynamicSyncFieldArrayBuilder)
        {
            this.dynamicSyncPropertyBuilder = dynamicSyncPropertyBuilder;
            this.dynamicSyncFieldBuilder = dynamicSyncFieldBuilder;
            this.dynamicSyncPropertyArrayBuilder = dynamicSyncPropertyArrayBuilder;
            this.dynamicSyncFieldArrayBuilder = dynamicSyncFieldArrayBuilder;
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
                usings.Add(propertyInfo.PropertyType.Namespace);

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
            }

            foreach (var fieldInfo in dynamicRegistryItem.Fields)
            {
                usings.Add(fieldInfo.FieldType.Namespace);
                if(!fieldInfo.FieldType.IsGenericType && !fieldInfo.FieldType.IsArray)
                { 
                    transpilers.Add(dynamicSyncFieldBuilder.GetTranspiler(fieldInfo));
                    messages.AddRange(dynamicSyncFieldBuilder.GetMessages(fieldInfo));
                    messageHandlers.Add(dynamicSyncFieldBuilder.GetSubscription(fieldInfo));
                }
                else if (fieldInfo.FieldType.IsArray)
                {
                    transpilers.Add(dynamicSyncFieldArrayBuilder.GetTranspiler(fieldInfo));
                    messages.AddRange(dynamicSyncFieldArrayBuilder.GetMessages(fieldInfo));
                    messageHandlers.Add(dynamicSyncFieldArrayBuilder.GetSubscription(fieldInfo));
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
    }
}
