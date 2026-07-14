using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Replays one tick's ordered mutations for a troop-roster element while carrying its roster and character
/// identities only once.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTroopRosterElementBatch : ICommand
{
    [ProtoMember(1)]
    public readonly string RosterId;

    [ProtoMember(2)]
    public readonly string CharacterId;

    [ProtoMember(3)]
    public readonly TroopRosterElementOperation[] Operations;

    public NetworkTroopRosterElementBatch(string rosterId, string characterId,
        TroopRosterElementOperation[] operations)
    {
        RosterId = rosterId;
        CharacterId = characterId;
        Operations = operations;
    }
}
