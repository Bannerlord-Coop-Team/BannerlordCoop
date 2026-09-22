using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages.Lifetime;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateBuilding : ICommand
{
    [ProtoMember(1)]
    public string BuildingId { get; }
    [ProtoMember(2)]
    public uint Handle { get; }

    public NetworkCreateBuilding(string buildingId, uint handle)
    {
        BuildingId = buildingId;
        Handle = handle;
    }
}
