using Common.Messaging;
using GameInterface.Services.Towns.ProtoSerializers;
using ProtoBuf;
using System.Collections.Generic;

namespace Coop.Core.Server.Services.Towns.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeTownSoldItems : IEvent
    {
        [ProtoMember(1)]
        public string TownId { get; }

        [ProtoMember(2)]
        public List<SellLogSerializer> LogList { get; }

        public NetworkChangeTownSoldItems(string townId, List<SellLogSerializer> logList)
        {
            TownId = townId;
            LogList = logList;
        }
    }
}
