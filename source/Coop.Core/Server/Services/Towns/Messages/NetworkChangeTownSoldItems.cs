using Common.Messaging;
using GameInterface.Services.Towns.Data;
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
        public SellLogData[] LogList { get; }

        public NetworkChangeTownSoldItems(string townId, SellLogData[] logList)
        {
            TownId = townId;
            LogList = logList;
        }
    }
}
