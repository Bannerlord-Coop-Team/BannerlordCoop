using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The server dropped a parked siege aftermath without applying it; the leader's client stops holding its choice.
/// </summary>
public readonly struct SiegeAftermathDiscarded : IEvent
{
    public readonly Settlement Settlement;

    public SiegeAftermathDiscarded(Settlement settlement)
    {
        Settlement = settlement;
    }
}
