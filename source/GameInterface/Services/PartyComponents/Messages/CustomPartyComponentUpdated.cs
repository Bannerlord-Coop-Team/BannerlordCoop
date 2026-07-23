using Common.Messaging;
using GameInterface.Services.PartyComponents.Patches.CustomPartyComponents;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages
{
    internal record CustomPartyComponentUpdated : IEvent
    {
        public CustomPartyComponent Component { get; set; }
        public CustomPartyComponentType ComponentType { get; set; }
        public string NewValue { get; set; }

        public CustomPartyComponentUpdated(CustomPartyComponent component, CustomPartyComponentType componentType, string newValue)
        {
            Component = component;
            ComponentType = componentType;
            NewValue = newValue;
        }
    }
}
