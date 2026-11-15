using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace Missions.Messages;

/// <summary>
/// Battle host to peers over the mission mesh: the battle's one AI fleet attempt was made for <see cref="Side"/>, and whether
/// it failed. A promoted host never fields the fleet again, and every client counts a failed side as fielded for the end hold.
/// The host also reports it to the server, which records it for players who join the battle later.
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
