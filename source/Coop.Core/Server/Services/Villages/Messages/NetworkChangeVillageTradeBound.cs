using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Villages.Messages;

/// <summary>
/// Used to notify of TradeBound changes
/// </summary>
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
