using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkBreakOutResult : IEvent
{
    [ProtoMember(1)] public string RequestId { get; }
    [ProtoMember(2)] public bool Approved { get; }
    [ProtoMember(3)] public uint[] CharacterIds { get; }
    [ProtoMember(4)] public int[] Counts { get; }
    [ProtoMember(5)] public int ArmyCasualties { get; }

    public NetworkBreakOutResult(string requestId, bool approved, uint[] characterIds, int[] counts, int armyCasualties)
    {
        RequestId = requestId;
        Approved = approved;
        CharacterIds = characterIds;
        Counts = counts;
        ArmyCasualties = armyCasualties;
    }
}
