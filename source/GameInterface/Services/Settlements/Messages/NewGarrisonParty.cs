using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

public readonly struct NewGarrisonParty : IEvent
{
    public readonly Settlement Settlement;

    public NewGarrisonParty(Settlement settlement)
    {
        Settlement = settlement;
    }
}
