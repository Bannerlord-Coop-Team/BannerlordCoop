using Common.Messaging;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Messages;

internal readonly struct VillagerPartyVillageChanged : IEvent
{
    public readonly VillagerPartyComponent Instance;
    public readonly Village Village;

    public VillagerPartyVillageChanged(VillagerPartyComponent instance, Village village)
    {
        Instance = instance;
        Village = village;
    }
}
