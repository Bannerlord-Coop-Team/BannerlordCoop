using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal interface ICaravanAmbushMapConversation
{
    bool IsOwnerFreeOnMap();
    void Open(MobileParty caravan);
}

internal class CaravanAmbushMapConversation : ICaravanAmbushMapConversation
{
    public bool IsOwnerFreeOnMap() => GameStateManager.Current?.ActiveState is MapState map && !map.AtMenu &&
        !map.IsSimulationActive && !Campaign.Current.ConversationManager.IsConversationInProgress;

    public void Open(MobileParty caravan)
    {
        var leader = ConversationHelper.GetConversationCharacterPartyLeader(caravan.Party);
        CampaignMapConversation.OpenConversation(
            new ConversationCharacterData(Hero.MainHero.CharacterObject), new ConversationCharacterData(leader));
    }
}
