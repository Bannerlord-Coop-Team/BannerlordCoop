using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Villages.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkChangeVillageTradeBound : IEvent
    {
        [ProtoMember(1)]
        public string VillageId { get; }
        [ProtoMember(2)]
        public string TradeBoundId { get; }

        public NetworkChangeVillageTradeBound(string villageId, string tradeBoundId)
        {
            VillageId = villageId;
            TradeBoundId = tradeBoundId;
        }
    }
}
