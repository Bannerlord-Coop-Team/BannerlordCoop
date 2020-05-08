using System.Collections.Generic;
using System.Reflection;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game.Persistence.Party
{
    public class MobilePartyEntityClient : RailEntityClient<MobilePartyState>
    {
        private readonly IEnvironment m_Environment;
        private readonly IDictionary<EntityId, MobileParty> m_Mapping;

        public MobilePartyEntityClient(
            IEnvironment environment,
            IDictionary<EntityId, MobileParty> mapping)
        {
            m_Environment = environment;
            m_Mapping = mapping;
        }

        protected override void OnAdded()
        {
            m_Mapping[Id] = m_Environment.GetMobilePartyByIndex(State.PartyId);
            PropertyInfo position = typeof(MobilePartyState).GetProperty(nameof(State.Position));
            m_Environment.AddRemoteMoveTo(m_Mapping[Id], new RemoteValue<Vec2>(State, position));
        }

        protected override void OnRemoved()
        {
            m_Mapping.Remove(Id);
            m_Environment.RemoveRemoteMoveTo(m_Mapping[Id]);
        }

        public override void PostUpdate()
        {
            if (IsControlled)
            {
                Vec2? vTarget = m_Environment.RemoteMoveTo[m_Mapping[Id]].DrainRequest();
                if (vTarget.HasValue)
                {
                    Room.RaiseEvent<EventPartyMoveTo>(
                        e =>
                        {
                            e.EntityId = Id;
                            e.Pos = vTarget.Value;
                        });
                }
            }
        }
    }

    public class MobilePartyEntityServer : RailEntityServer<MobilePartyState>
    {
        private readonly IEnvironment m_Environment;
        private readonly IDictionary<EntityId, MobileParty> m_Mapping;

        public MobilePartyEntityServer(
            IEnvironment environment,
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
