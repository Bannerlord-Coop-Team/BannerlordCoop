using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models.Collections
{
	public class CollectionHandlerTemplateModel : CollectionTemplateModel, ITemplateModel
	{
		public override string TemplateFileName => "CollectionHandlerTemplate";

		public override string GetOutputPath() => GetMainProjectPath(@$"Gameinterface\Services\{TypeName}s\Handlers\{CollectionName}Handler.cs");

		public CollectionHandlerTemplateModel(ServiceTypeInfo serviceInfo, MemberInfo selectedCollection) : base(selectedCollection)
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
