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
    public class AddedMessageTemplateModel : CollectionTemplateModel, ITemplateModel
    {
        public override string TemplateFileName => "AddedMessageTemplate";

        public override string GetOutputPath() => GetRelativeDirectory(@$"Gameinterface\Services\{TypeName}s\Messages\Collections\{CollectionName}Added.cs");

        public AddedMessageTemplateModel(ServiceTypeInfo serviceInfo, MemberInfo selectedCollection) : base(selectedCollection)
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
