using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;
[ProtoContract(SkipConstructor = true)]
internal class NetworkFinalizeMilitiaPartyComponent : ICommand
{
    public NetworkFinalizeMilitiaPartyComponent(string componentId)
    {
        ComponentId = componentId;
    }

    [ProtoMember(1)]
    public string ComponentId { get; }

}
