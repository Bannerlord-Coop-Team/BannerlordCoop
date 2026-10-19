using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.SiegeEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkChangeSiegeBlockade : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string SiegeEventId;

    [ProtoMember(2)]
    public readonly bool IsBlockadeActive;

    public NetworkChangeSiegeBlockade(string siegeEventId, bool isBlockadeActive)
    {
        SiegeEventId = siegeEventId;
        IsBlockadeActive = isBlockadeActive;
    }
}
