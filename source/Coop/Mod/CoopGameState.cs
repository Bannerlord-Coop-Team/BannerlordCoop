using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    public class CoopGameState
    {
        private readonly HashSet<MobileParty> m_PlayerParties;

        public CoopGameState()
        {
            m_PlayerParties = new HashSet<MobileParty>();
        }

        public void AddPlayerControllerParty(MobileParty party)
        {
            if (party == null)
            {
                throw new ArgumentNullException();
            }

            m_PlayerParties.Add(party);
        }

        public bool IsPlayerControlledParty(MobileParty party)
        {
            return m_PlayerParties.Contains(party);
        }
    }
}
