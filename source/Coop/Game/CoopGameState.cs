using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    public class CoopGameState
    {
        public CoopGameState()
        {
            m_PlayerParties = new HashSet<MobileParty>();
        }
        public void AddPlayerControllerParty(MobileParty party)
        {
            if(party == null)
            {
                throw new ArgumentNullException();
            }
            m_PlayerParties.Add(party);
        }
        public bool IsPlayerControlledParty(MobileParty party)
        {
            return m_PlayerParties.Contains(party);
        }
        private readonly HashSet<MobileParty> m_PlayerParties;
    }
}
