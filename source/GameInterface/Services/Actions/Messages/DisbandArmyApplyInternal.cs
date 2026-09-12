using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Actions.Messages;

/// <summary>
/// Event for when an army is disbanded
/// </summary>
public readonly struct DisbandArmyApplyInternal : IEvent
{
    public readonly Army Army;
    public readonly MobileParty ClientParty;

    public DisbandArmyApplyInternal(Army army, MobileParty clientParty)
    {
        Army = army;
        ClientParty = clientParty;
    }
}
