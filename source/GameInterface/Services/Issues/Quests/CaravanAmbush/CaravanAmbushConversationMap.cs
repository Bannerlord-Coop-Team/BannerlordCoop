using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal interface ICaravanAmbushConversationMap
{
    bool IsOwnerFreeOnMap();
    bool TryOpen(MobileParty caravan);
}

internal class CaravanAmbushConversationMap : ICaravanAmbushConversationMap
{
    public bool IsOwnerFreeOnMap() => GameStateManager.Current?.ActiveState is MapState map && !map.AtMenu &&
        !map.IsSimulationActive && !Campaign.Current.ConversationManager.IsConversationInProgress;

    public bool TryOpen(MobileParty caravan)
    {
        var leader = ConversationHelper.GetConversationCharacterPartyLeader(caravan.Party);
        if (leader == null) return false;
        CampaignMapConversation.OpenConversation(
            new ConversationCharacterData(Hero.MainHero.CharacterObject), new ConversationCharacterData(leader));
        return true;
    }
}
