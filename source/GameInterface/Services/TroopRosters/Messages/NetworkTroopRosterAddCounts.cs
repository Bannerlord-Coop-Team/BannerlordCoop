using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Legacy single-operation form for adding or subtracting counts on one identity-keyed roster element.
/// Normal authority traffic carries this operation inside <see cref="NetworkTroopRosterElementBatch"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTroopRosterAddCounts : ICommand
{
    [ProtoMember(1)]
    public readonly string RosterId;
    [ProtoMember(2)]
    public readonly string CharacterId;
    [ProtoMember(3)]
    public readonly int Count;
    [ProtoMember(4)]
    public readonly int WoundedCount;
    [ProtoMember(5)]
    public readonly int XpChange;
    [ProtoMember(6)]
    public readonly bool RemoveDepleted;

    public NetworkTroopRosterAddCounts(string rosterId, string characterId, int count, int woundedCount, int xpChange, bool removeDepleted)
    {
        RosterId = rosterId;
        CharacterId = characterId;
        Count = count;
        WoundedCount = woundedCount;
        XpChange = xpChange;
        RemoveDepleted = removeDepleted;
    }
}
