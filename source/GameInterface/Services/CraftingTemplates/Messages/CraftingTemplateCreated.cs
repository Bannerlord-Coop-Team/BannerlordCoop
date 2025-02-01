using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingTemplates.Messages
{
    internal record CraftingTemplateCreated : IEvent
    {
        public CraftingTemplate CraftingTemplate { get; }

        public CraftingTemplateCreated(CraftingTemplate craftingTemplate)
        {
            CraftingTemplate = craftingTemplate;
        }
    }
}
