using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.SiegeEnginesContainers.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateSiegeEnginesContainer : ICommand
{
    [ProtoMember(1)]
    public string SiegeEnginesContainerId { get; }

    public NetworkCreateSiegeEnginesContainer(string siegeEnginesId)
    {
        SiegeEnginesContainerId = siegeEnginesId;
    }
}