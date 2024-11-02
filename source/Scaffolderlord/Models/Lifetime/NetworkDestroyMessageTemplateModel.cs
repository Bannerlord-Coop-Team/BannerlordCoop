using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models.Lifetime
{
	public class NetworkDestroyMessageTemplateModel : BaseTemplateModel, ITemplateModel
	{
		public string TypeName { get; }
		public string? Namespace { get; }
		public IEnumerable<string> Usings { get; }

		public string TemplateFileName => @"Lifetime\NetworkDestroyMessageTemplate.cshtml";

		public string GetOutputPath() => GetRelativeDirectory(@$"Gameinterface\Services\{TypeName}s\Messages\Lifetime\NetworkDestroy{TypeName}.cs");

		public NetworkDestroyMessageTemplateModel(ServiceTypeInfo serviceInfo)
		{
			TypeName = serviceInfo.Type.Name;
			Namespace = $"GameInterface.Services.{TypeName}s.Messages;";
            Usings = GetUsings(serviceInfo.Type);
        }
	}
}
