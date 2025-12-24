using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Buildings.Messages;

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