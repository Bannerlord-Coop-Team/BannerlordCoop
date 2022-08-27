using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Network
{
    [ProtoContract]
    public class NetworkAgent
    {


        public Agent Agent { get; private set; }

        [ProtoMember(1)]
        public Guid NetworkId { get; private set; }

        public NetworkAgent(Agent agent)
        {
            Agent = agent;
        }


        public void Move()
        {
            throw new NotImplementedException();
        }

        public void OnDeath()
        {
            throw new NotImplementedException();
        }
    }
}
