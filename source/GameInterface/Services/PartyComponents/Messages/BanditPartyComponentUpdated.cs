using Common.Messaging;
using GameInterface.Services.PartyComponents.Patches.BanditPartyComponents;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages
{
    internal record BanditPartyComponentUpdated : IEvent
    {
        public BanditPartyComponent Component { get; set; }
        public BanditPartyComponentType ComponentType { get; set; }
        public string NewValue { get; set; }

        public BanditPartyComponentUpdated(BanditPartyComponent component, BanditPartyComponentType componentType, string newValue)
        {
            Component = component;
            ComponentType = componentType;
            NewValue = newValue;
        }
    }
}
