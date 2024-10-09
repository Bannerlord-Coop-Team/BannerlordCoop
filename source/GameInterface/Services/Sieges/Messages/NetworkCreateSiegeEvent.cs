using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Sieges.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateSiegeEvent : ICommand
{
    [ProtoMember(1)]
    public string SiegeId { get; }

    public NetworkCreateSiegeEvent(string siegeId)
    {
        SiegeId = siegeId;
    }
}
