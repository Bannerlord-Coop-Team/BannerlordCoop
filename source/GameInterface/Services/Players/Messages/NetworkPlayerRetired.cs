using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Players.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestPlayerRetirement : ICommand {}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerRetired : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly string HeroId;

    public NetworkPlayerRetired(string controllerId, string heroId)
    {
        ControllerId = controllerId;
        HeroId = heroId;
    }
}
