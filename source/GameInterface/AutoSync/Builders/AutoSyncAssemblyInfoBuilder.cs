using GameInterface.AutoSync.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameInterface.AutoSync.Builders
{
    public class AutoSyncAssemblyInfoBuilder
    {
        private readonly AutoSyncRegistry autoSyncRegistry;

        public AutoSyncAssemblyInfoBuilder(AutoSyncRegistry autoSyncRegistry)
        {
            this.autoSyncRegistry = autoSyncRegistry;
        }
        public SyntaxTree Build(IEnumerable<string> assemblies)
        {
            var ignoreCheckAccessAssemblies = new HashSet<Assembly>();
            foreach (var registration in autoSyncRegistry.Registrations)
            {
                var dynamicRegistryItem = registration.Value;
                ignoreCheckAccessAssemblies.Add(registration.Key.Assembly);

                foreach (var fieldInfo in dynamicRegistryItem.Fields)
                {
                    ignoreCheckAccessAssemblies.Add(fieldInfo.Value.FieldType.Assembly);
                }

                foreach (var propertyInfo in dynamicRegistryItem.Properties)
                {
                    ignoreCheckAccessAssemblies.Add(propertyInfo.Value.PropertyType.Assembly);
                }

                foreach (var targetMethod in dynamicRegistryItem.TargetMethods)
                {
                    ignoreCheckAccessAssemblies.Add(targetMethod.DeclaringType.Assembly);
                }
            }

            var assemblyInfoTemplate = TemplateParser.Parse("AutoAssemblyInfoTemplate", new
            {
                Assemblies = ignoreCheckAccessAssemblies.Distinct().Select(a => a.GetName().Name).Concat(assemblies)
            });

            AutoSyncConfiguration.ExportFile("AssemblyInfo.cs", assemblyInfoTemplate);

            return CSharpSyntaxTree.ParseText(assemblyInfoTemplate);
        }
    }
}
