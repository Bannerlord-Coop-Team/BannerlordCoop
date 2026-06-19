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
/// Replicates TroopRoster changes as identity-keyed per-operation deltas (the ItemRoster pattern) — the
/// alternative to the whole-roster snapshot. On the authority each roster mutation publishes a local
/// event carrying the SERVER index; this handler resolves the element's identity from the server's
/// (always-aligned) roster and sends a message keyed by that identity, never by array index. The client
/// applies it through the same vanilla mutators, finding-or-creating the element by identity, so it stays
/// correct regardless of the client roster's layout and self-heals an under-populated client roster.
/// </summary>
/// <remarks>
/// No array index crosses the wire, so the index-out-of-range storm the per-index sync caused cannot
/// recur. Messages send immediately on the authority's own patches (no per-frame coalescer pump), so
/// unlike the snapshot this also replicates on a headless host. Positional reorders
/// (ShiftTroopToIndex/SwapTroopsAtIndices) are display ordering and are intentionally not synced.
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

    #region Authority send path

    private void Handle_CountsAtIndexAdded(MessagePayload<CountsAtIndexAdded> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Index, out var rosterId, out var characterId, out var isHero)) return;
        network.SendAll(new NetworkTroopRosterAddCounts(rosterId, characterId, isHero, e.CountChange, e.WoundedCountChange, e.XpChange, e.RemoveDepleted));
    }

    private void Handle_ElementNumberSet(MessagePayload<ElementNumberSet> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Index, out var rosterId, out var characterId, out var isHero)) return;
        network.SendAll(new NetworkTroopRosterSetNumber(rosterId, characterId, isHero, e.Number));
    }

    private void Handle_ElementWoundedNumberSet(MessagePayload<ElementWoundedNumberSet> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Index, out var rosterId, out var characterId, out var isHero)) return;
        network.SendAll(new NetworkTroopRosterSetWoundedNumber(rosterId, characterId, isHero, e.Number));
    }

    private void Handle_ElementXpSet(MessagePayload<ElementXpSet> payload)
    {
        var e = payload.What;
        if (!TryResolve(e.TroopRoster, e.Index, out var rosterId, out var characterId, out var isHero)) return;
        network.SendAll(new NetworkTroopRosterSetXp(rosterId, characterId, isHero, e.Number));
    }

    private void Handle_ZeroCountsRemoved(MessagePayload<ZeroCountsRemoved> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.TroopRoster, out var rosterId)) return;
        network.SendAll(new NetworkTroopRosterRemoveZeroCounts(rosterId));
    }

    /// <summary>
    /// Resolves the roster id and the identity (hero id, or basic-troop CharacterObject id) of the
    /// element at <paramref name="index"/> on the authority's roster, where the index is always valid.
    /// </summary>
    private bool TryResolve(TroopRoster roster, int index, out string rosterId, out string characterId, out bool isHero)
    {
        rosterId = null;
        characterId = null;
        isHero = false;
        if (roster == null) return false;
        if (!objectManager.TryGetIdWithLogging(roster, out rosterId)) return false;
        if (index < 0 || index >= roster.Count) return false;

        var character = roster.GetElementCopyAtIndex(index).Character;
        if (character == null) return false;

        var hero = character.HeroObject;
        isHero = hero != null;
        return isHero
            ? objectManager.TryGetIdWithLogging(hero, out characterId)
            : objectManager.TryGetIdWithLogging(character, out characterId);
    }

    #endregion

    #region Client apply path

    private void Handle_NetworkAddCounts(MessagePayload<NetworkTroopRosterAddCounts> payload)
    {
        var m = payload.What;
        Apply(m.RosterId, m.CharacterId, m.IsHero, nameof(NetworkTroopRosterAddCounts),
            (roster, character) => roster.AddToCounts(character, m.Count, false, m.WoundedCount, m.XpChange, m.RemoveDepleted));
    }

    private void Handle_NetworkSetNumber(MessagePayload<NetworkTroopRosterSetNumber> payload)
    {
        var m = payload.What;
        Apply(m.RosterId, m.CharacterId, m.IsHero, nameof(NetworkTroopRosterSetNumber),
            (roster, character) => roster.SetElementNumber(FindOrCreateIndex(roster, character), m.Number));
    }

    private void Handle_NetworkSetWoundedNumber(MessagePayload<NetworkTroopRosterSetWoundedNumber> payload)
    {
        var m = payload.What;
        Apply(m.RosterId, m.CharacterId, m.IsHero, nameof(NetworkTroopRosterSetWoundedNumber),
            (roster, character) => roster.SetElementWoundedNumber(FindOrCreateIndex(roster, character), m.Number));
    }

    private void Handle_NetworkSetXp(MessagePayload<NetworkTroopRosterSetXp> payload)
    {
        var m = payload.What;
        Apply(m.RosterId, m.CharacterId, m.IsHero, nameof(NetworkTroopRosterSetXp),
            (roster, character) => roster.SetElementXp(FindOrCreateIndex(roster, character), m.Xp));
    }

    private void Handle_NetworkRemoveZeroCounts(MessagePayload<NetworkTroopRosterRemoveZeroCounts> payload)
    {
        var rosterId = payload.What.RosterId;
        GameThread.Run(() =>
        {
            using (new AllowedThread())
            {
                try
                {
                    if (!objectManager.TryGetObjectWithLogging<TroopRoster>(rosterId, out var roster)) return;
                    roster.RemoveZeroCounts();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to apply {Message}. RosterId: {RosterId}", nameof(NetworkTroopRosterRemoveZeroCounts), rosterId);
                }
            }
        });
    }

    /// <summary>
    /// Resolves the roster and element identity on the client and applies <paramref name="apply"/> through
    /// vanilla mutators on the game thread under <see cref="AllowedThread"/>, so the apply does not
    /// re-trigger the authority patches. Resolution runs inside the game loop too, so it stays ordered
    /// behind any deferred create of the roster or character.
    /// </summary>
    private void Apply(string rosterId, string characterId, bool isHero, string messageName, Action<TroopRoster, CharacterObject> apply)
    {
        GameThread.Run(() =>
        {
            using (new AllowedThread())
            {
                try
                {
                    if (!objectManager.TryGetObjectWithLogging<TroopRoster>(rosterId, out var roster)) return;

                    CharacterObject character;
                    if (isHero)
                    {
                        if (!objectManager.TryGetObjectWithLogging<Hero>(characterId, out var hero)) return;
                        character = hero.CharacterObject;
                    }
                    else if (!objectManager.TryGetObjectWithLogging<CharacterObject>(characterId, out character))
                    {
                        return;
                    }

                    apply(roster, character);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to apply {Message}. RosterId: {RosterId} CharacterId: {CharacterId}", messageName, rosterId, characterId);
                }
            }
        });
    }

    /// <summary>
    /// Returns the index of <paramref name="character"/> in <paramref name="roster"/>, creating an empty
    /// element for it first if absent, so an absolute Set for an element the client does not yet have
    /// still applies (self-healing). Runs under AllowedThread, so AddNewElement does not re-publish.
    /// </summary>
    private static int FindOrCreateIndex(TroopRoster roster, CharacterObject character)
    {
        int index = roster.FindIndexOfTroop(character);
        if (index < 0)
        {
            roster.AddNewElement(character, -1);
            index = roster.FindIndexOfTroop(character);
        }
        return index;
    }

    #endregion
}
