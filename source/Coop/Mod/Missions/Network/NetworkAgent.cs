using Coop.NetImpl.LiteNet;
using LiteNetLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace Coop.Mod.Missions
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
