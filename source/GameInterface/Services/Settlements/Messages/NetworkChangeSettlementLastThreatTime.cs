using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Settlements.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkChangeSettlementLastThreatTime : IEvent
{

}
