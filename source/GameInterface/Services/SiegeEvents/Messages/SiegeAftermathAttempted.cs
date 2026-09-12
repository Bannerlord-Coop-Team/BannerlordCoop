using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The local player picked a siege aftermath (devastate, pillage or show mercy); ask the server to apply it.
/// </summary>
public readonly struct SiegeAftermathAttempted : IEvent
{
    public readonly MobileParty Party;
    public readonly Settlement Settlement;
    public readonly int AftermathType;

    public SiegeAftermathAttempted(MobileParty party, Settlement settlement, int aftermathType)
    {
        Party = party;
        Settlement = settlement;
        AftermathType = aftermathType;
    }
}
