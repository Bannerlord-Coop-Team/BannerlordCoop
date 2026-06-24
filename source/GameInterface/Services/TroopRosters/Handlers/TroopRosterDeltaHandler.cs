using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Handlers;

/// <summary>
/// Replicates TroopRoster changes as identity-keyed per-operation deltas (the ItemRoster pattern). On the
/// authority each roster mutation publishes a local event carrying the SERVER index; this handler resolves
/// the element's identity from the server's (always-aligned) roster and sends a message keyed by that
/// identity, never by array index. The client applies it through the same vanilla mutators, found by
/// identity, so it stays correct regardless of the client roster's layout. A positive AddCounts creates the
/// element if it is missing (vanilla AddToCounts find-or-creates and keeps the cached totals correct); the
/// absolute Set deltas require the element to already exist (its create is its own earlier, reliably-ordered
/// delta) and are skipped otherwise, since minting a placeholder here would corrupt the roster's cached
/// totals and be wiped by RemoveZeroCounts.
/// </summary>
/// <remarks>
/// No array index crosses the wire, so it cannot land out of range on an under-populated client roster.
/// Messages send immediately on the authority's own patches, so it replicates on a headless host.
/// Positional reorders (ShiftTroopToIndex/SwapTroopsAtIndices) are display ordering and are intentionally
/// not synced.
/// </remarks>
internal class TroopRosterDeltaHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterDeltaHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public TroopRosterDeltaHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

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
    }

    private void Handle_CountsAtIndexAdded(MessagePayload<CountsAtIndexAdded> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Character, out var rosterId, out var characterId)) return;
        network.SendAll(new NetworkTroopRosterAddCounts(rosterId, characterId, e.CountChange, e.WoundedCountChange, e.XpChange, e.RemoveDepleted));
    }

    private void Handle_ElementNumberSet(MessagePayload<ElementNumberSet> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Character, out var rosterId, out var characterId)) return;
        network.SendAll(new NetworkTroopRosterSetNumber(rosterId, characterId, e.Number));
    }

    private void Handle_ElementWoundedNumberSet(MessagePayload<ElementWoundedNumberSet> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Character, out var rosterId, out var characterId)) return;
        network.SendAll(new NetworkTroopRosterSetWoundedNumber(rosterId, characterId, e.Number));
    }

    private void Handle_ElementXpSet(MessagePayload<ElementXpSet> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Character, out var rosterId, out var characterId)) return;
        network.SendAll(new NetworkTroopRosterSetXp(rosterId, characterId, e.Number));
    }

    private void Handle_ZeroCountsRemoved(MessagePayload<ZeroCountsRemoved> payload)
    {
        // Resolve silently: an unregistered roster is a scratch/dummy roster (see TryResolve) with nothing
        // to replicate, not an error.
        if (!objectManager.TryGetId(payload.What.TroopRoster, out var rosterId)) return;
        network.SendAll(new NetworkTroopRosterRemoveZeroCounts(rosterId));
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
        return objectManager.TryGetIdWithLogging(character, out characterId);
    }

    private void Handle_NetworkAddCounts(MessagePayload<NetworkTroopRosterAddCounts> payload)
    {
        var m = payload.What;
        Apply(m.RosterId, m.CharacterId, nameof(NetworkTroopRosterAddCounts), (roster, character) =>
        {
            // A subtract for a troop this client doesn't have yet can't apply: vanilla AddToCounts asserts and
            // does nothing when the element is absent and the net change is <= 0. The add that creates the
            // element is its own earlier delta, so skip rather than trip the assert.
            if (m.Count + m.WoundedCount <= 0 && roster.FindIndexOfTroop(character) < 0)
            {
                Logger.Debug("Skipped {Message}: {Character} is not in roster {Roster} yet", nameof(NetworkTroopRosterAddCounts), m.CharacterId, m.RosterId);
                return;
            }
            roster.AddToCounts(character, m.Count, false, m.WoundedCount, m.XpChange, m.RemoveDepleted);
        });
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
        Apply(rosterId, characterId, messageName, (roster, character) =>
        {
            int index = roster.FindIndexOfTroop(character);
            if (index < 0)
            {
                Logger.Debug("Skipped {Message}: {Character} is not in roster {Roster} yet", messageName, characterId, rosterId);
                return;
            }
            apply(roster, index);
        });
    }
}
