using System.Reflection;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models
{
    public class AutoSyncTemplateModel : BaseTemplateModel, ITemplateModel
    {
        public string TypeName { get; }
        public string? Namespace { get; }
        public IEnumerable<string> Usings { get; set; }
        public IEnumerable<FieldInfo> Fields { get; }
        public IEnumerable<PropertyInfo> Properties { get; }
        public IEnumerable<MemberInfo> Collections { get; }

        public virtual string TemplateFileName => "AutoSyncTemplate.cshtml";

        public virtual string GetOutputPath() => GetRelativeDirectory(@$"Gameinterface\Services\{TypeName}s\{TypeName}Sync.cs");

        public AutoSyncTemplateModel(ServiceTypeInfo serviceInfo)
        {
            TypeName = serviceInfo.Type.Name;
            Namespace = $"GameInterface.Services.{serviceInfo.Type.Name}s;";
            Usings = new[]
            {
                serviceInfo.Type.Namespace!
            };

            Fields = serviceInfo.Fields;
            Properties = serviceInfo.Properties;
            Collections = serviceInfo.Collections;
            GetStaticUsings();
        }

        private void GetStaticUsings()
        {
            var members = Fields.Cast<MemberInfo>().Concat(Properties.Cast<MemberInfo>());
            Usings = Usings.Concat(GetStaticUsings(members));
        }
    }
}
