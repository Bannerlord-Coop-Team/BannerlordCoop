using ProtoBuf;

namespace Coop.Naval.PortScreen.Data;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ShipRenameData
{
    [ProtoMember(1)]
    public readonly uint ShipHandle;

    [ProtoMember(2)]
    public readonly string NewName;

    public ShipRenameData(
        uint shipHandle,
        string newName)
    {
        ShipHandle = shipHandle;
        NewName = newName;
    }
}
