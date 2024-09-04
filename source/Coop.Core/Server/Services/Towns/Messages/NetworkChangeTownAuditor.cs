using Common.Messaging;
using GameInterface.Services.Towns.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.Towns.Messages
{
    /// <summary>
    /// Server sends this data when a Town Auditor is sent from the debug command.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeTownAuditor : IEvent
    {
        [ProtoMember(1)]
        public TownAuditorData[] Datas { get; }
        
        public NetworkChangeTownAuditor(TownAuditorData[] townAuditorDatas)
        {
            Datas = townAuditorDatas;
        }
    }
}
