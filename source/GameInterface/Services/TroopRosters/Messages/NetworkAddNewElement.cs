using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddNewElement : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;

    [ProtoMember(2)]
    public readonly string ObjectId;

    [ProtoMember(3)]
    public readonly int InsertionIndex;

    public NetworkAddNewElement(string troopRosterId, string objectId, int insertionIndex)
    {
        TroopRosterId = troopRosterId;
        ObjectId = objectId;
        InsertionIndex = insertionIndex;
    }
}
