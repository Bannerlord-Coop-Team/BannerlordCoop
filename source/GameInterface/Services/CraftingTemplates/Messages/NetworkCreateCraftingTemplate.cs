using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.CraftingTemplates.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateCraftingTemplate : ICommand
    {
        [ProtoMember(1)]
        public string CraftingTemplateId { get; }

        public NetworkCreateCraftingTemplate(string craftingTemplateId)
        {
            CraftingTemplateId = craftingTemplateId;
        }
    }
}