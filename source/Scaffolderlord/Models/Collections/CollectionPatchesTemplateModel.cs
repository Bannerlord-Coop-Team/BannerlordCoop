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
	public class CollectionPatchesTemplateModel : CollectionTemplateModel, ITemplateModel
	{
		public override string TemplateFileName => "CollectionPatchesTemplate";

		public override string GetOutputPath() => GetRelativeDirectory(@$"Gameinterface\Services\{TypeName}s\Patches\{CollectionName}Patches.cs");

		public CollectionPatchesTemplateModel(ServiceTypeInfo serviceInfo, MemberInfo selectedCollection) : base(selectedCollection)
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
