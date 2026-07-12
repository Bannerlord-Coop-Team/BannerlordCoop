using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using GameInterface.Services.TroopRosters.Coalescing;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Handlers;

/// <summary>
/// Replicates TroopRoster changes as identity-keyed operations. AddCounts and XP mutations for one regular
/// troop share an ordered per-tick batch; roster-wide and other absolute mutations flush that batch before
/// sending. Hero AddCounts operations send immediately because their party-linkage side effects must preserve
/// source/destination order across different rosters. The client resolves each element by identity and replays
/// the same vanilla mutators in order.
/// </summary>
/// <remarks>
/// No array index crosses the wire, so it cannot land out of range on an under-populated client roster.
/// The authority's own patches enqueue directly, so replication works on a headless host.
/// Positional reorders (ShiftTroopToIndex/SwapTroopsAtIndices) are display ordering and are intentionally
/// not synced.
/// </remarks>
internal class TroopRosterDeltaHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterDeltaHandler>();
    private const string ElementBatchChannel = "TroopRosterElementBatch";

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISendCoalescer coalescer;

    public TroopRosterDeltaHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network,
        ISendCoalescer coalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.coalescer = coalescer;

        // Authority send path: the roster patches publish these local events (server-only) with the server index.
        messageBroker.Subscribe<CountsAtIndexAdded>(Handle_CountsAtIndexAdded);
        messageBroker.Subscribe<ElementNumberSet>(Handle_ElementNumberSet);
        messageBroker.Subscribe<ElementWoundedNumberSet>(Handle_ElementWoundedNumberSet);
        messageBroker.Subscribe<ElementXpSet>(Handle_ElementXpSet);
        messageBroker.Subscribe<ZeroCountsRemoved>(Handle_ZeroCountsRemoved);

        // Client apply path.
        messageBroker.Subscribe<NetworkTroopRosterAddCounts>(Handle_NetworkAddCounts);
        messageBroker.Subscribe<NetworkTroopRosterSetNumber>(Handle_NetworkSetNumber);
        messageBroker.Subscribe<NetworkTroopRosterSetWoundedNumber>(Handle_NetworkSetWoundedNumber);
        messageBroker.Subscribe<NetworkTroopRosterSetXp>(Handle_NetworkSetXp);
        messageBroker.Subscribe<NetworkTroopRosterRemoveZeroCounts>(Handle_NetworkRemoveZeroCounts);
        messageBroker.Subscribe<NetworkTroopRosterElementBatch>(Handle_NetworkElementBatch);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CountsAtIndexAdded>(Handle_CountsAtIndexAdded);
        messageBroker.Unsubscribe<ElementNumberSet>(Handle_ElementNumberSet);
        messageBroker.Unsubscribe<ElementWoundedNumberSet>(Handle_ElementWoundedNumberSet);
        messageBroker.Unsubscribe<ElementXpSet>(Handle_ElementXpSet);
        messageBroker.Unsubscribe<ZeroCountsRemoved>(Handle_ZeroCountsRemoved);

        messageBroker.Unsubscribe<NetworkTroopRosterAddCounts>(Handle_NetworkAddCounts);
        messageBroker.Unsubscribe<NetworkTroopRosterSetNumber>(Handle_NetworkSetNumber);
        messageBroker.Unsubscribe<NetworkTroopRosterSetWoundedNumber>(Handle_NetworkSetWoundedNumber);
        messageBroker.Unsubscribe<NetworkTroopRosterSetXp>(Handle_NetworkSetXp);
        messageBroker.Unsubscribe<NetworkTroopRosterRemoveZeroCounts>(Handle_NetworkRemoveZeroCounts);
        messageBroker.Unsubscribe<NetworkTroopRosterElementBatch>(Handle_NetworkElementBatch);
    }

    private void Handle_CountsAtIndexAdded(MessagePayload<CountsAtIndexAdded> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Character, out var rosterId, out var characterId)) return;

        var operation = TroopRosterElementOperation.AddCounts(
            e.CountChange, e.WoundedCountChange, e.XpChange, e.RemoveDepleted);

        if (e.Character.IsHero)
        {
            // AddToCounts mutates Hero.PartyBelongedTo / PartyBelongedToAsPrisoner. Keeping hero deltas in
            // separate (roster, character) coalescer keys lets Dictionary slot reuse reorder a transfer's
            // destination add ahead of its source remove. Flush earlier work for this roster, then send this
            // hero operation immediately so the reliable stream retains the authority's cross-roster order.
            coalescer?.FlushInstance(rosterId, network);
            network.SendAll(new NetworkTroopRosterElementBatch(rosterId, characterId,
                new[] { operation }));
            return;
        }

        Enqueue(rosterId, characterId, operation);
    }

    private void Handle_ElementNumberSet(MessagePayload<ElementNumberSet> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Character, out var rosterId, out var characterId)) return;
        coalescer?.FlushInstance(rosterId, network);
        network.SendAll(new NetworkTroopRosterSetNumber(rosterId, characterId, e.Number));
    }

    private void Handle_ElementWoundedNumberSet(MessagePayload<ElementWoundedNumberSet> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Character, out var rosterId, out var characterId)) return;
        coalescer?.FlushInstance(rosterId, network);
        network.SendAll(new NetworkTroopRosterSetWoundedNumber(rosterId, characterId, e.Number));
    }

    private void Handle_ElementXpSet(MessagePayload<ElementXpSet> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Character, out var rosterId, out var characterId)) return;
        Enqueue(rosterId, characterId, TroopRosterElementOperation.SetXp(e.Number));
    }

    private void Handle_ZeroCountsRemoved(MessagePayload<ZeroCountsRemoved> payload)
    {
        // Resolve silently: an unregistered roster is a scratch/dummy roster (see TryResolve) with nothing
        // to replicate, not an error.
        if (!objectManager.TryGetId(payload.What.TroopRoster, out var rosterId)) return;
        rosterId = Compact(rosterId, typeof(TroopRoster));
        coalescer?.FlushInstance(rosterId, network);
        network.SendAll(new NetworkTroopRosterRemoveZeroCounts(rosterId));
    }

    private void Enqueue(string rosterId, string characterId, TroopRosterElementOperation operation)
    {
        if (coalescer == null)
        {
            network.SendAll(new NetworkTroopRosterElementBatch(rosterId, characterId,
                new[] { operation }));
            return;
        }

        var key = new CoalesceKey(ElementBatchChannel, rosterId, characterId);
        coalescer.Enqueue(key, new TroopRosterElementBatchPayload(rosterId, characterId, operation));
    }

    /// <summary>
    /// Resolves the roster id and the element's CharacterObject id. Every character, hero or basic troop, is
    /// registered by its CharacterObject, so one lookup keys both. The character is captured by the patch while
    /// its index was still valid, so this never reads a post-mutation index and a removal still names the right
    /// troop. The roster is resolved silently: an unregistered roster is a transient one with no network
    /// identity (an AI party not synced to this client, or a battle-simulation / tooltip dummy roster) and
    /// nothing to replicate, so it is skipped rather than logged as an error - a battle mutates thousands of
    /// such scratch rosters and the per-miss error log floods the game thread.
    /// </summary>
    private bool TryResolve(TroopRoster roster, CharacterObject character, out string rosterId, out string characterId)
    {
        rosterId = null;
        characterId = null;
        if (roster == null || character == null) return false;
        if (!objectManager.TryGetId(roster, out rosterId)) return false;
        if (!objectManager.TryGetIdWithLogging(character, out characterId)) return false;
        rosterId = Compact(rosterId, typeof(TroopRoster));
        characterId = Compact(characterId, typeof(CharacterObject));
        return true;
    }

    private void Handle_NetworkAddCounts(MessagePayload<NetworkTroopRosterAddCounts> payload)
    {
        var m = payload.What;
        Apply(m.RosterId, m.CharacterId, nameof(NetworkTroopRosterAddCounts),
            (roster, character) => ApplyAddCounts(roster, character, m.RosterId, m.CharacterId,
                m.Count, m.WoundedCount, m.XpChange, m.RemoveDepleted));
    }

    private void ApplyAddCounts(TroopRoster roster, CharacterObject character, string rosterId,
        string characterId, int count, int woundedCount, int xpChange, bool removeDepleted)
    {
        int index = roster.FindIndexOfTroop(character);

        // A subtract for a troop this client doesn't have yet can't apply: vanilla AddToCounts asserts and
        // does nothing when the element is absent and the net change is <= 0. The add that creates the
        // element is its own earlier delta, so skip rather than trip the assert.
        if (count + woundedCount <= 0 && index < 0)
        {
            Logger.Debug("Skipped {Message}: {Character} is not in roster {Roster} yet",
                nameof(NetworkTroopRosterAddCounts), characterId, rosterId);
            return;
        }

        // Clamp a duplicate or replayed removal so client counts cannot go negative. The authority should
        // never produce this case, so keep the detailed error that identifies the offending element.
        if (index >= 0)
        {
            var current = roster.GetElementCopyAtIndex(index);
            if (current.Number + count < 0 || current.WoundedNumber + woundedCount < 0)
            {
                Logger.Error("Over-subtract {Message} for {Character} in roster {Roster}: have (number={Number}, wounded={Wounded}), requested delta (number={Count}, wounded={WoundedCount}). Clamping to zero - the authority sent a duplicate remove (double-call upstream).",
                    nameof(NetworkTroopRosterAddCounts), characterId, rosterId, current.Number,
                    current.WoundedNumber, count, woundedCount);

                if (current.Number + count < 0) count = -current.Number;
                if (current.WoundedNumber + woundedCount < 0) woundedCount = -current.WoundedNumber;
            }
        }

        roster.AddToCounts(character, count, false, woundedCount, xpChange, removeDepleted);
    }

    private void Handle_NetworkSetNumber(MessagePayload<NetworkTroopRosterSetNumber> payload)
    {
        var m = payload.What;
        ApplyToExisting(m.RosterId, m.CharacterId, nameof(NetworkTroopRosterSetNumber),
            (roster, index) => roster.SetElementNumber(index, m.Number));
    }

    private void Handle_NetworkSetWoundedNumber(MessagePayload<NetworkTroopRosterSetWoundedNumber> payload)
    {
        var m = payload.What;
        ApplyToExisting(m.RosterId, m.CharacterId, nameof(NetworkTroopRosterSetWoundedNumber),
            (roster, index) => roster.SetElementWoundedNumber(index, m.Number));
    }

    private void Handle_NetworkSetXp(MessagePayload<NetworkTroopRosterSetXp> payload)
    {
        var m = payload.What;
        ApplyToExisting(m.RosterId, m.CharacterId, nameof(NetworkTroopRosterSetXp),
            (roster, index) => roster.SetElementXp(index, m.Xp));
    }

    private void Handle_NetworkElementBatch(MessagePayload<NetworkTroopRosterElementBatch> payload)
    {
        var m = payload.What;
        if (m.Operations == null || m.Operations.Length == 0) return;

        Apply(m.RosterId, m.CharacterId, nameof(NetworkTroopRosterElementBatch), (roster, character) =>
        {
            foreach (var operation in m.Operations)
            {
                switch (operation.Kind)
                {
                    case TroopRosterElementOperationKind.AddCounts:
                        ApplyAddCounts(roster, character, m.RosterId, m.CharacterId, operation.Count,
                            operation.WoundedCount, operation.Xp, operation.RemoveDepleted);
                        break;
                    case TroopRosterElementOperationKind.SetXp:
                        ApplyToExisting(roster, character, m.RosterId, m.CharacterId,
                            nameof(NetworkTroopRosterSetXp),
                            (existingRoster, index) => existingRoster.SetElementXp(index, operation.Xp));
                        break;
                    default:
                        Logger.Error("Unknown troop-roster batch operation {OperationKind} for {Character} in roster {Roster}",
                            operation.Kind, m.CharacterId, m.RosterId);
                        break;
                }
            }
        });
    }

    private void Handle_NetworkRemoveZeroCounts(MessagePayload<NetworkTroopRosterRemoveZeroCounts> payload)
    {
        var rosterId = payload.What.RosterId;
        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                if (!objectManager.TryGetObjectWithLogging<TroopRoster>(rosterId, out var roster)) return;
                roster.RemoveZeroCounts();
            }
        }, context: nameof(NetworkTroopRosterRemoveZeroCounts));
    }

    /// <summary>
    /// Resolves the roster and element identity on the client and applies <paramref name="apply"/> through
    /// vanilla mutators on the game thread under <see cref="AllowedThread"/>, so the apply does not
    /// re-trigger the authority patches. Resolution runs inside the game loop too, so it stays ordered
    /// behind any deferred create of the roster or character.
    /// </summary>
    private void Apply(string rosterId, string characterId, string messageName, Action<TroopRoster, CharacterObject> apply)
    {
        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                if (!objectManager.TryGetObjectWithLogging<TroopRoster>(rosterId, out var roster)) return;
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(characterId, out var character)) return;

                apply(roster, character);
            }
        }, context: messageName);
    }

    /// <summary>
    /// Resolves the roster and element (via <see cref="Apply"/>) and runs <paramref name="apply"/> only when
    /// the element already exists in the roster. An absent element means this client is under-populated for
    /// that troop; the create that adds it is its own earlier, reliably-ordered delta. We deliberately do NOT
    /// create a placeholder for an absolute Set: SetElementNumber/WoundedNumber/Xp do not maintain the
    /// roster's cached totals (only AddToCounts does), so a placeholder would under-count TotalManCount and be
    /// wiped by the next RemoveZeroCounts. Skipping keeps the client consistent until the create arrives.
    /// </summary>
    private void ApplyToExisting(string rosterId, string characterId, string messageName, Action<TroopRoster, int> apply)
    {
        Apply(rosterId, characterId, messageName,
            (roster, character) => ApplyToExisting(roster, character, rosterId, characterId,
                messageName, apply));
    }

    private void ApplyToExisting(TroopRoster roster, CharacterObject character, string rosterId,
        string characterId, string messageName, Action<TroopRoster, int> apply)
    {
        int index = roster.FindIndexOfTroop(character);
        if (index < 0)
        {
            Logger.Debug("Skipped {Message}: {Character} is not in roster {Roster} yet",
                messageName, characterId, rosterId);
            return;
        }

        apply(roster, index);
    }
}
