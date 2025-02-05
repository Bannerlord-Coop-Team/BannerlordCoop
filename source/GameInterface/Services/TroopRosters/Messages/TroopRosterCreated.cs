using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages
{
    internal class TroopRosterCreated : IEvent
    {
        public TroopRoster TroopRoster { get; }
        public PartyBase PartyBase { get; }

        public TroopRosterCreated(TroopRoster troopRoster, PartyBase partyBase = null)
        {
            TroopRoster = troopRoster;
            PartyBase = partyBase;
        }
    }
}
