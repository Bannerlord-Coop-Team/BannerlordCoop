using System.Collections.Generic;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.Party
{
    public class MobilePartyEntityServer : RailEntityServer<MobilePartyState>
    {
        private readonly IEnvironmentServer m_Environment;
        private readonly IDictionary<EntityId, MobileParty> m_Mapping;

        public MobilePartyEntityServer(
            IEnvironmentServer environment,
            IDictionary<EntityId, MobileParty> mapping)
        {
            m_Environment = environment;
            m_Mapping = mapping;
        }

        protected override void OnAdded()
        {
            m_Mapping[Id] = m_Environment.GetMobilePartyByIndex(State.PartyId);
        }

        protected override void OnRemoved()
        {
            m_Mapping.Remove(Id);
        }
    }
}
