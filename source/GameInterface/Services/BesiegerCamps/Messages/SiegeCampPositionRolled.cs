using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.BesiegerCamps.Messages;

/// <summary>
/// The server rolled a besieging party's camp position; clients snap their copy to it.
/// </summary>
public readonly struct SiegeCampPositionRolled : IEvent
{
    public readonly MobileParty Party;
    public readonly CampaignVec2 Position;

    public SiegeCampPositionRolled(MobileParty party, CampaignVec2 position)
    {
        Party = party;
        Position = position;
    }
}
