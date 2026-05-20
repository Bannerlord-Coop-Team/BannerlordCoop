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

        public readonly List<FieldInfo> ReadOnlyFields = new List<FieldInfo>();

        public DynamicSyncConstantsBuilder()
        {
        }

        public int AddReadonlyField(FieldInfo fieldInfo)
        {
            if (ReadOnlyFields.Contains(fieldInfo))
                return ReadOnlyFields.IndexOf(fieldInfo);

            ReadOnlyFields.Add(fieldInfo);
            return ReadOnlyFields.Count - 1;
        }

        public SyntaxTree Build()
        {
            var items = InterfaceTypes.OrderBy(t => t.Name).ToList();
            var constantsTemplate = TemplateParser.Parse("DynamicConstantsTemplate", new
            {
                Types = items.Select(i => new { Type = i, Index = items.IndexOf(i) }),
                Libraries = ReadOnlyFields.SelectMany(f => DynamicSyncUtils.GetLibraries(f)).Distinct().ToList(),
                ReadOnlyFields = ReadOnlyFields
            });

            DynamicSyncConfiguration.ExportFile("Constants.cs", constantsTemplate);

            return CSharpSyntaxTree.ParseText(constantsTemplate);
        }
    }
}
