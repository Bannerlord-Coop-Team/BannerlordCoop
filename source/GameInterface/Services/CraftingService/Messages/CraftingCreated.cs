using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.CraftingService.Messages
{
    internal record CraftingCreated : IEvent
    {
        public Crafting Crafting;
        public CraftingTemplate CraftingTemplate;
        public BasicCultureObject CultureObject;
        public TextObject TextObject;

        public CraftingCreated(Crafting crafting, CraftingTemplate craftingTemplate, BasicCultureObject cultureObject, TextObject textObject)
        {
            Crafting = crafting;
            CraftingTemplate = craftingTemplate;
            CultureObject = cultureObject;
            TextObject = textObject;
        }
    }
}
