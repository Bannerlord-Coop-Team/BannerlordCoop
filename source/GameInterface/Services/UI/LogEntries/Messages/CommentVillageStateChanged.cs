using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.UI.LogEntries.Messages;

public readonly struct CommentVillageStateChanged : IEvent
{
    public readonly Village Village;
    public readonly Village.VillageStates OldState;
    public readonly Village.VillageStates NewState;
    public readonly MobileParty RaiderParty;

    public CommentVillageStateChanged(
        Village village,
        Village.VillageStates oldState,
        Village.VillageStates newState,
        MobileParty raiderParty)
    {
        Village = village;
        OldState = oldState;
        NewState = newState;
        RaiderParty = raiderParty;
    }
}
