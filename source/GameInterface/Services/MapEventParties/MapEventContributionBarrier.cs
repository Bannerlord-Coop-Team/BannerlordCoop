using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties;

/// <summary>
/// Flushes pending absolute contribution values before dependent battle-result or teardown traffic.
/// </summary>
internal static class MapEventContributionBarrier
{
    public static void Flush(MapEvent mapEvent)
    {
        if (mapEvent == null ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<ISendCoalescer>(out var coalescer) ||
            !ContainerProvider.TryResolve<INetwork>(out var network))
            return;

        Flush(mapEvent, objectManager, coalescer, network);
    }

    internal static void Flush(
        MapEvent mapEvent,
        IObjectManager objectManager,
        ISendCoalescer coalescer,
        INetwork network)
    {
        if (mapEvent == null || objectManager == null || coalescer == null || network == null)
            return;

        var flushedIds = new HashSet<string>();
        FlushSide(mapEvent.AttackerSide, objectManager, coalescer, network, flushedIds);
        FlushSide(mapEvent.DefenderSide, objectManager, coalescer, network, flushedIds);
    }

    private static void FlushSide(
        MapEventSide side,
        IObjectManager objectManager,
        ISendCoalescer coalescer,
        INetwork network,
        HashSet<string> flushedIds)
    {
        if (side == null) return;

        foreach (MapEventParty party in side.Parties)
        {
            if (!objectManager.TryGetId(party, out var partyId) || !flushedIds.Add(partyId))
                continue;

            coalescer.FlushInstance(partyId, network);
        }
    }
}
