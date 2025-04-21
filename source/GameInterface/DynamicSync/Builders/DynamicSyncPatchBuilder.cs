using GameInterface.DynamicSync.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPatchBuilder
    {
        private readonly DynamicSyncPropertyBuilder dynamicSyncPropertyBuilder;
        private readonly DynamicSyncFieldBuilder dynamicSyncFieldBuilder;

        public DynamicSyncPatchBuilder(DynamicSyncPropertyBuilder dynamicSyncPropertyBuilder, DynamicSyncFieldBuilder dynamicSyncFieldBuilder)
        {
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
                usings.Add(propertyInfo.PropertyType.Namespace);
                prefixes.Add(dynamicSyncPropertyBuilder.GetPrefix(propertyInfo));
                messages.AddRange(dynamicSyncPropertyBuilder.GetMessages(propertyInfo));
                messageHandlers.Add(dynamicSyncPropertyBuilder.GetSubscription(propertyInfo));
            }

            foreach (var fieldInfo in dynamicRegistryItem.Fields)
            {
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
    }
}
