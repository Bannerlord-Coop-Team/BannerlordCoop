using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships.Messages;

/// <summary>
/// Server to every client: the AI fleet attempt the server recorded for <see cref="Side"/> of a naval battle, which a player
/// who opens that battle later caps its own fleet against.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNpcFleetAttemptRecorded : IEvent
{
    [ProtoMember(1)] public readonly string InstanceId;
    [ProtoMember(2)] public readonly BattleSideEnum Side;
    // The AI hulls the attempt left in the battle: none once it failed.
    [ProtoMember(3)] public readonly int AiHulls;
    // The players' fielded hulls the AI share left room for, and those players' party ids; empty arrives as null.
    [ProtoMember(4)] public readonly int PlayerHulls;
    [ProtoMember(5)] public readonly string[] CountedPartyIds;

    public NetworkNpcFleetAttemptRecorded(string instanceId, BattleSideEnum side, int aiHulls, int playerHulls,
        string[] countedPartyIds)
    {
        InstanceId = instanceId;
        Side = side;
        AiHulls = aiHulls;
        PlayerHulls = playerHulls;
        CountedPartyIds = countedPartyIds;
    }
}
