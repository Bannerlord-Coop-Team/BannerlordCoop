using GameInterface.DynamicSync.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncAssemblyInfoBuilder
    {
        private readonly DynamicSyncRegistry dynamicSyncRegistry;

        public DynamicSyncAssemblyInfoBuilder(DynamicSyncRegistry dynamicSyncRegistry)
        {
            this.dynamicSyncRegistry = dynamicSyncRegistry;
        }
        public SyntaxTree Build(IEnumerable<string> assemblies)
        {
            List<Assembly> ignoreCheckAccessAssemblies = new List<Assembly>();
            foreach (var registration in dynamicSyncRegistry.Registrations)
            {
                var dynamicRegistryItem = registration.Value;
                ignoreCheckAccessAssemblies.Add(registration.Key.Assembly);

                foreach (var fieldInfo in dynamicRegistryItem.Fields)
                {
                    ignoreCheckAccessAssemblies.Add(fieldInfo.FieldType.Assembly);
                }

                foreach (var propertyInfo in dynamicRegistryItem.Properties)
                {
                    ignoreCheckAccessAssemblies.Add(propertyInfo.PropertyType.Assembly);
                }

                foreach (var targetMethod in dynamicRegistryItem.TargetMethods)
                {
                    ignoreCheckAccessAssemblies.Add(targetMethod.DeclaringType.Assembly);
                }
            }

                var assemblyInfoTemplate = TemplateParser.Parse("DynamicAssemblyInfoTemplate", new
            {
                Assemblies = ignoreCheckAccessAssemblies.Distinct().Select(a => a.GetName().Name).Concat(assemblies)
            });

            DynamicSyncConfiguration.ExportFile("AssemblyInfo.cs", assemblyInfoTemplate);

            return CSharpSyntaxTree.ParseText(assemblyInfoTemplate);
        }
    }
}
