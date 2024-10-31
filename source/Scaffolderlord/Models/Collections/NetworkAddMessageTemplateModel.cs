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
	public class NetworkAddMessageTemplateModel : CollectionTemplateModel, ITemplateModel
	{
		public override string TemplateFileName => "NetworkAddMessageTemplate";

		public override string GetOutputPath() => GetRelativeDirectory(@$"Gameinterface\Services\{TypeName}s\Messages\Collections\Network{CollectionName}Add.cs");

		public NetworkAddMessageTemplateModel(ServiceTypeInfo serviceInfo, MemberInfo selectedCollection) : base(selectedCollection)
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
