using Scaffolderlord.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models.E2E
{
    public class E2EFieldTestsTemplateModel : BaseTemplateModel, ITemplateModel
    {
        public string TypeName { get; }
        public string? Namespace { get; }
        public IEnumerable<string> Usings { get; set; }
        public IEnumerable<FieldInfo> Fields { get; }

        public string TemplateFileName => @"E2E\E2EFieldTestsTemplate.cshtml";

        public string GetOutputPath() => GetRelativeDirectory(@$"E2E.Tests\Services\{TypeName}s\{TypeName}FieldTests.cs");

        public IEnumerable<FieldInfo> GetStructFields() => Fields.Where(x => x.FieldType.IsStruct());
        public IEnumerable<FieldInfo> GetClassFields() => Fields.Where(x => !x.FieldType.IsStruct());

        public E2EFieldTestsTemplateModel(ServiceTypeInfo serviceInfo)
        {
            TypeName = serviceInfo.Type.Name;
            Namespace = $"E2E.Tests.Services.{serviceInfo.Type.Name}s;";
            Fields = serviceInfo.Fields;

            Usings = Fields
                .Select(p => p.FieldType.Namespace)
                .Append(serviceInfo.Type.Namespace)
                .Concat(GetStaticUsings(Fields))
                .OfType<string>()
                .Distinct();
        }
    }
}
