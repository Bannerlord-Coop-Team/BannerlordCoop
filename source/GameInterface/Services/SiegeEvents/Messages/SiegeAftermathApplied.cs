using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// The server applied a siege aftermath; clients set their settlement-taken menu narration from it.
/// </summary>
public readonly struct SiegeAftermathApplied : IEvent
{
    public readonly Settlement Settlement;
    public readonly int AftermathType;

    public SiegeAftermathApplied(Settlement settlement, int aftermathType)
    {
        Settlement = settlement;
        AftermathType = aftermathType;
    }
}
