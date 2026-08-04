using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// A garrison sally-out map event started on the server; besieging players need to be seated on it.
/// </summary>
/// <remarks>
/// A sortie inverts the siege sides: vanilla's CheckSallyOut calls
/// StartPartyEncounter(garrison.Party, BesiegerCamp.LeaderParty.Party), so the GARRISON is the map event's
/// attacker and the BESIEGERS are its defenders. Only the camp leader is passed in, and MapEvent.Initialize's
/// camp sweep skips any member that is in an army without being its leader, so co-besieging players are
/// routinely left off the event entirely.
/// </remarks>
public readonly struct SallyOutStarted : IEvent
{
    public readonly MobileParty GarrisonParty;
    public readonly Settlement Settlement;

    public SallyOutStarted(MobileParty garrisonParty, Settlement settlement)
    {
        GarrisonParty = garrisonParty;
        Settlement = settlement;
    }
}
