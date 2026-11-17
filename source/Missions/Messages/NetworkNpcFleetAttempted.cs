using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace Missions.Messages;

/// <summary>
/// Battle host to peers over the mission mesh: the battle's one AI fleet attempt was made for <see cref="Side"/>, and whether
/// it failed. A promoted host never fields the fleet again, and every client counts a failed side as fielded for the end hold.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNpcFleetAttempted : IEvent
{
    [ProtoMember(1)] public readonly BattleSideEnum Side;
    [ProtoMember(2)] public readonly bool Failed;

    public NetworkNpcFleetAttempted(BattleSideEnum side, bool failed)
    {
        Side = side;
        Failed = failed;
    }
}
