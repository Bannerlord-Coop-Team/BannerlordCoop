using GameInterface.AutoSync.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace GameInterface.AutoSync.Builders
{
    public class AutoSyncConstantsBuilder
    {

        public readonly HashSet<Type> InterfaceTypes = new HashSet<Type>();

        public readonly List<FieldInfo> ReadOnlyFields = new List<FieldInfo>();

        public AutoSyncConstantsBuilder()
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
            var constantsTemplate = TemplateParser.Parse("AutoConstantsTemplate", new
            {
                Types = items.Select(i => new { Type = i, Index = items.IndexOf(i) }),
                Libraries = ReadOnlyFields.SelectMany(f => AutoSyncUtils.GetLibraries(f)).Distinct().ToList(),
                ReadOnlyFields = ReadOnlyFields.Select(f => new {
                    DeclaringTypeName = AutoSyncUtils.GetSimpleTypeName(f.DeclaringType),
                    Name = f.Name
                }).ToList()
            });

            AutoSyncConfiguration.ExportFile("Constants.cs", constantsTemplate);

            return CSharpSyntaxTree.ParseText(constantsTemplate);
        }
    }
}
