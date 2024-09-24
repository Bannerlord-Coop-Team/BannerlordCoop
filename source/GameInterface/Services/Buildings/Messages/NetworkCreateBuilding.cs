using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages.Lifetime;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateBuilding : ICommand
{
    [ProtoMember(1)]
    public string BuildingId { get; }

    public NetworkCreateBuilding(string buildingId)
    {
        BuildingId = buildingId;
    }
}
