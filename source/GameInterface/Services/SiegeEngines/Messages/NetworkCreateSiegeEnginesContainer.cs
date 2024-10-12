using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.SiegeEnginesContainers.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateSiegeEnginesContainer : ICommand
{
    [ProtoMember(1)]
    public string SiegeEnginesId { get; }

    [ProtoMember(2)]
    public string SiegeConstructionProgressId { get; }

    public NetworkCreateSiegeEnginesContainer(string siegeEnginesId, string siegeConstructionProgressId)
    {
        SiegeEnginesId = siegeEnginesId;
        SiegeConstructionProgressId = siegeConstructionProgressId;
    }
}