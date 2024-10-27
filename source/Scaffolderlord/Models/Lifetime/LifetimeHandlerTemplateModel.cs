using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models.Lifetime
{
    public class LifetimeHandlerTemplateModel : BaseTemplateModel, ITemplateModel
    {
        public string TypeName { get; }
        public string? Namespace { get; }
        public string[] Usings { get; }

        public string TemplateFileName => @"Lifetime\LifetimeHandlerTemplate.cshtml";

        public string GetOutputPath() => GetMainProjectPath(@$"Gameinterface\Services\{TypeName}s\Handlers\{TypeName}LifetimeHandler.cs");

        public LifetimeHandlerTemplateModel(ServiceTypeInfo serviceInfo)
        {
            TypeName = serviceInfo.Type.Name;
            Namespace = $"GameInterface.Services.{TypeName}s.Handlers;";
            Usings = new[]
            {
                serviceInfo.Type.Namespace!
            };
        }
    }
}
