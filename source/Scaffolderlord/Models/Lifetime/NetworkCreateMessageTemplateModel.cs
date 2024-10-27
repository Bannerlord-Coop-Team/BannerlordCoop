using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models.Lifetime
{
    public class NetworkCreateMessageTemplateModel : BaseTemplateModel, ITemplateModel
    {
        public string TypeName { get; }
        public string? Namespace { get; }
        public string[] Usings { get; }

        public string TemplateFileName => @"Lifetime\NetworkCreateMessageTemplate.cshtml";

        public string GetOutputPath() => GetMainProjectPath(@$"Gameinterface\Services\{TypeName}s\Messages\Lifetime\NetworkCreate{TypeName}.cs");

        public NetworkCreateMessageTemplateModel(ServiceTypeInfo serviceInfo)
        {
            TypeName = serviceInfo.Type.Name;
            Namespace = $"GameInterface.Services.{TypeName}s.Messages;";
            Usings = new[]
            {
                serviceInfo.Type.Namespace!
            };
        }
    }
}
