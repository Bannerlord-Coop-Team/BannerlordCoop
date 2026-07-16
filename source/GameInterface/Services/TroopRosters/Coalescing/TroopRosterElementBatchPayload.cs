using Common.Messaging;
using Common.Network.Coalescing;
using GameInterface.Services.TroopRosters.Messages;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.TroopRosters.Coalescing;

/// <summary>
/// Accumulates ordered mutations for one roster element. Only adjacent absolute XP sets collapse; an
/// AddCounts operation remains an ordering boundary because it can change XP or remove the element.
/// </summary>
internal sealed class TroopRosterElementBatchPayload : ICoalescedPayload
{
    private readonly string rosterId;
    private readonly string characterId;
    private readonly List<TroopRosterElementOperation> operations = new();

    public TroopRosterElementBatchPayload(string rosterId, string characterId,
        TroopRosterElementOperation operation)
    {
        this.rosterId = rosterId;
        this.characterId = characterId;
        operations.Add(operation);
    }

    public ICoalescedPayload Merge(ICoalescedPayload incoming)
    {
        if (incoming is not TroopRosterElementBatchPayload other)
        {
            throw new ArgumentException(
                $"Cannot merge {incoming?.GetType().Name ?? "null"} into {nameof(TroopRosterElementBatchPayload)}; " +
                "a coalesce key must use a single payload type.",
                nameof(incoming));
        }

        if (!string.Equals(rosterId, other.rosterId, StringComparison.Ordinal) ||
            !string.Equals(characterId, other.characterId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Cannot merge troop-roster batches for different elements.",
                nameof(incoming));
        }

        if (ReferenceEquals(this, other)) return this;

        foreach (var operation in other.operations)
        {
            Append(operation);
        }

        return this;
    }

    public IMessage ToMessage() =>
        new NetworkTroopRosterElementBatch(rosterId, characterId, operations.ToArray());

    private void Append(TroopRosterElementOperation operation)
    {
        int lastIndex = operations.Count - 1;
        if (lastIndex >= 0 &&
            operations[lastIndex].Kind == TroopRosterElementOperationKind.SetXp &&
            operation.Kind == TroopRosterElementOperationKind.SetXp)
        {
            operations[lastIndex] = operation;
            return;
        }

        operations.Add(operation);
    }
}
