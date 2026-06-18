using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Handlers;

/// <summary>
/// Replicates whole <see cref="TroopRoster"/> contents from the authority to clients.
/// </summary>
/// <remarks>
/// On the authority, any content change published by <see cref="Patches.TroopRosterPatches"/> marks
/// the roster dirty in <see cref="TroopRosterSyncCoalescer"/>; the coalescer flushes one snapshot per
/// roster per frame back through <see cref="FlushDirtyRosters"/>. On a client, a snapshot is applied
/// by rebuilding the roster through <see cref="ITroopRosterInterface.UpdateWithData"/>, which restores
/// the count, cached totals, and materialized element list correctly and preserves the local
/// main hero's slot. This replaces the per-index delta path, which could index past an
/// under-populated client roster.
/// </remarks>
internal class TroopRosterSnapshotHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterSnapshotHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly TroopRosterSyncCoalescer coalescer;

    public TroopRosterSnapshotHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface,
        TroopRosterSyncCoalescer coalescer)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;
        this.coalescer = coalescer;

        messageBroker.Subscribe<CountsAtIndexAdded>(Handle_CountsAtIndexAdded);
        messageBroker.Subscribe<NewElementAdded>(Handle_NewElementAdded);
        messageBroker.Subscribe<ZeroCountsRemoved>(Handle_ZeroCountsRemoved);
        messageBroker.Subscribe<ElementNumberSet>(Handle_ElementNumberSet);
        messageBroker.Subscribe<ElementWoundedNumberSet>(Handle_ElementWoundedNumberSet);
        messageBroker.Subscribe<ElementXpSet>(Handle_ElementXpSet);
        messageBroker.Subscribe<TroopShiftedToIndex>(Handle_TroopShiftedToIndex);
        messageBroker.Subscribe<TroopsSwappedAtIndices>(Handle_TroopsSwappedAtIndices);

        messageBroker.Subscribe<NetworkUpdateTroopRoster>(Handle_NetworkUpdateTroopRoster);

        coalescer.Flush = FlushDirtyRosters;
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CountsAtIndexAdded>(Handle_CountsAtIndexAdded);
        messageBroker.Unsubscribe<NewElementAdded>(Handle_NewElementAdded);
        messageBroker.Unsubscribe<ZeroCountsRemoved>(Handle_ZeroCountsRemoved);
        messageBroker.Unsubscribe<ElementNumberSet>(Handle_ElementNumberSet);
        messageBroker.Unsubscribe<ElementWoundedNumberSet>(Handle_ElementWoundedNumberSet);
        messageBroker.Unsubscribe<ElementXpSet>(Handle_ElementXpSet);
        messageBroker.Unsubscribe<TroopShiftedToIndex>(Handle_TroopShiftedToIndex);
        messageBroker.Unsubscribe<TroopsSwappedAtIndices>(Handle_TroopsSwappedAtIndices);

        messageBroker.Unsubscribe<NetworkUpdateTroopRoster>(Handle_NetworkUpdateTroopRoster);

        coalescer.Flush = null;
    }

    private void Handle_CountsAtIndexAdded(MessagePayload<CountsAtIndexAdded> payload) => MarkDirty(payload.What.TroopRoster);
    private void Handle_NewElementAdded(MessagePayload<NewElementAdded> payload) => MarkDirty(payload.What.TroopRoster);
    private void Handle_ZeroCountsRemoved(MessagePayload<ZeroCountsRemoved> payload) => MarkDirty(payload.What.TroopRoster);
    private void Handle_ElementNumberSet(MessagePayload<ElementNumberSet> payload) => MarkDirty(payload.What.TroopRoster);
    private void Handle_ElementWoundedNumberSet(MessagePayload<ElementWoundedNumberSet> payload) => MarkDirty(payload.What.TroopRoster);
    private void Handle_ElementXpSet(MessagePayload<ElementXpSet> payload) => MarkDirty(payload.What.TroopRoster);
    private void Handle_TroopShiftedToIndex(MessagePayload<TroopShiftedToIndex> payload) => MarkDirty(payload.What.TroopRoster);
    private void Handle_TroopsSwappedAtIndices(MessagePayload<TroopsSwappedAtIndices> payload) => MarkDirty(payload.What.TroopRoster);

    private void MarkDirty(TroopRoster roster)
    {
        // Only the authority sends snapshots; on a client these change events never fire (the patches
        // stand down), so this is a defensive guard rather than a live path.
        if (ModInformation.IsClient) return;

        coalescer.MarkDirty(roster);
    }

    private void FlushDirtyRosters(IReadOnlyList<TroopRoster> rosters)
    {
        foreach (var roster in rosters)
        {
            if (!objectManager.TryGetIdWithLogging(roster, out var rosterId)) continue;

            var data = troopRosterInterface.PackTroopRosterData(roster);
            network.SendAll(new NetworkUpdateTroopRoster(rosterId, data));
        }
    }

    private void Handle_NetworkUpdateTroopRoster(MessagePayload<NetworkUpdateTroopRoster> payload)
    {
        var message = payload.What;

        GameThread.Run(() =>
        {
            using (new AllowedThread())
            {
                try
                {
                    if (!objectManager.TryGetObjectWithLogging<TroopRoster>(message.RosterId, out var troopRoster)) return;

                    troopRosterInterface.UpdateWithData(troopRoster, message.Data, Hero.MainHero);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to apply {Message}. TroopRosterId: {TroopRosterId}", nameof(NetworkUpdateTroopRoster), message.RosterId);
                }
            }
        });
    }
}
