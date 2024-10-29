using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models
{
	public class RegistryTemplateModel : BaseTemplateModel, ITemplateModel
	{
		public string TypeName { get; }
		public string? Namespace { get; }
		public IEnumerable<string> Usings { get; }

		public string TemplateFileName => "RegistryTemplate.cshtml";

		public string GetOutputPath() => GetMainProjectPath(@$"Gameinterface\Services\{TypeName}s\{TypeName}Registry.cs");

		public RegistryTemplateModel(ServiceTypeInfo serviceInfo)
		{
			TypeName = serviceInfo.Type.Name;
			Namespace = $"GameInterface.Services.{serviceInfo.Type.Name}s;";
			Usings = new[]
			{
				serviceInfo.Type.Namespace!
			};
		}
	}
}
