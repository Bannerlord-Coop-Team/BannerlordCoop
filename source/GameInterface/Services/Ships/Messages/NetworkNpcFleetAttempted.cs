using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships.Messages;

/// <summary>
/// The battle's one AI fleet attempt for <see cref="Side"/>, sent by the battle host to its peers and to the server, which keeps
/// it and passes it on to every client: a promoted host never fields the fleet again, a failed side releases the end hold, and a
/// player who joins later caps its own fleet at what the side's eight ships have left.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNpcFleetAttempted : IEvent
{
    [ProtoMember(1)] public readonly BattleSideEnum Side;
    [ProtoMember(2)] public readonly bool Failed;
    [ProtoMember(3)] public readonly string InstanceId;
    // The AI hulls the attempt left in the battle: none once it failed.
    [ProtoMember(4)] public readonly int AiHulls;
    // The players' fielded hulls the AI share left room for, and those players' party ids; empty arrives as null.
    [ProtoMember(5)] public readonly int PlayerHulls;
    [ProtoMember(6)] public readonly string[] CountedPartyIds;

    public NetworkNpcFleetAttempted(string instanceId, BattleSideEnum side, bool failed, int aiHulls, int playerHulls,
        string[] countedPartyIds)
    {
        InstanceId = instanceId;
        Side = side;
        Failed = failed;
        AiHulls = aiHulls;
        PlayerHulls = playerHulls;
        CountedPartyIds = countedPartyIds;
    }
}
