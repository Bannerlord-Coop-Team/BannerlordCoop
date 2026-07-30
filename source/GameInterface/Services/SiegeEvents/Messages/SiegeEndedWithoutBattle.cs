using Common.Messaging;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// A siege dissolved on the server without an assault battle. Participants are captured before
/// vanilla destroys the siege graph so clients can unwind the matching local siege UI.
/// </summary>
public readonly struct SiegeEndedWithoutBattle : IEvent
{
    public readonly Settlement Settlement;
    public readonly bool BesiegerDefeated;
    public readonly MobileParty LeaderParty;
    public readonly MobileParty[] AttackerParties;
    public readonly MobileParty[] DefenderParties;

    public SiegeEndedWithoutBattle(
        Settlement settlement,
        bool besiegerDefeated,
        MobileParty leaderParty,
        MobileParty[] attackerParties,
        MobileParty[] defenderParties)
    {
        Settlement = settlement;
        BesiegerDefeated = besiegerDefeated;
        LeaderParty = leaderParty;
        AttackerParties = attackerParties ?? Array.Empty<MobileParty>();
        DefenderParties = defenderParties ?? Array.Empty<MobileParty>();
    }
}
