using GameInterface.DynamicSync.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncConstantsBuilder
    {

        public readonly HashSet<Type> InterfaceTypes = new HashSet<Type>();

        public DynamicSyncConstantsBuilder()
        {
        }

        public SyntaxTree Build()
        {
            var items = InterfaceTypes.OrderBy(t => t.Name).ToList();
            var constantsTemplate = TemplateParser.Parse("DynamicConstantsTemplate", new
            {
                Types = items.Select(i => new { Type = i, Index = items.IndexOf(i) })
            });

            DynamicSyncConfiguration.ExportFile("Constants.cs", constantsTemplate);

            return CSharpSyntaxTree.ParseText(constantsTemplate);
        }
    }
}
