using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models.Lifetime
{
    public class LifetimePatchesTemplateModel : BaseTemplateModel, ITemplateModel
    {
        public string TypeName { get; }
        public string? Namespace { get; }
        public IEnumerable<string> Usings { get; }

        public string TemplateFileName => @"Lifetime\LifetimePatchesTemplate.cshtml";

        public string GetOutputPath() => GetMainProjectPath(@$"Gameinterface\Services\{TypeName}s\Patches\{TypeName}LifetimePatches.cs");

        public LifetimePatchesTemplateModel(ServiceTypeInfo serviceInfo)
        {
            TypeName = serviceInfo.Type.Name;
            Namespace = $"GameInterface.Services.{TypeName}s.Patches;";
            Usings = new[]
            {
                serviceInfo.Type.Namespace!
            };
        }
    }
}
