using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Replays one tick's ordered mutations for a troop-roster element while carrying its roster and character
/// identities only once.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTroopRosterElementBatch : ICommand, IJoinCatchUpMergeMessage
{
    private const int MaxJoinCatchUpOperations = 32;
    [ProtoMember(1)]
    public readonly string RosterId;

    [ProtoMember(2)]
    public readonly string CharacterId;

    [ProtoMember(3)]
    public readonly TroopRosterElementOperation[] Operations;

    public string JoinCatchUpMergeKey => $"{RosterId}:{CharacterId}";

    public bool TryMerge(IMessage next, out IMessage merged)
    {
        merged = null;
        if (next is not NetworkTroopRosterElementBatch other ||
            RosterId != other.RosterId || CharacterId != other.CharacterId ||
            Operations == null || other.Operations == null ||
            Operations.Length == 0 || other.Operations.Length == 0 ||
            Operations.Length > MaxJoinCatchUpOperations - other.Operations.Length)
            return false;

        // Keep every clamping, removal and XP operation in order; bound a single game-thread apply.
        var operations = new TroopRosterElementOperation[Operations.Length + other.Operations.Length];
        Array.Copy(Operations, operations, Operations.Length);
        Array.Copy(other.Operations, 0, operations, Operations.Length, other.Operations.Length);
        merged = new NetworkTroopRosterElementBatch(RosterId, CharacterId, operations);
        return true;
    }

    public NetworkTroopRosterElementBatch(string rosterId, string characterId,
        TroopRosterElementOperation[] operations)
    {
        RosterId = rosterId;
        CharacterId = characterId;
        Operations = operations;
    }
}
