using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models
{
	public class AutoSyncTemplateModel : BaseTemplateModel, ITemplateModel
	{
		public string TypeName { get; }
		public string? Namespace { get; }
		public IEnumerable<string> Usings { get; }

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
		}
	}
}
