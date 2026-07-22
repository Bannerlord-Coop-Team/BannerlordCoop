using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties;

/// <summary>
/// Flushes pending absolute contribution values before dependent battle-result or teardown traffic.
/// </summary>
internal interface IMapEventContributionBarrier
{
    void Flush(MapEvent mapEvent);

    void Flush(MapEventParty mapEventParty);
}

/// <inheritdoc cref="IMapEventContributionBarrier"/>
internal sealed class MapEventContributionBarrier : IMapEventContributionBarrier
{
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISendCoalescer coalescer;

    public MapEventContributionBarrier(
        IObjectManager objectManager,
        INetwork network,
        ISendCoalescer coalescer = null)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.coalescer = coalescer;
    }

    public void Flush(MapEvent mapEvent)
    {
        if (mapEvent == null || coalescer == null) return;

        var flushedIds = new HashSet<string>();
        FlushSide(mapEvent.AttackerSide, flushedIds);
        FlushSide(mapEvent.DefenderSide, flushedIds);
    }

    public void Flush(MapEventParty mapEventParty)
    {
        if (mapEventParty == null || coalescer == null) return;
        if (!objectManager.TryGetId(mapEventParty, out var partyId)) return;

        coalescer.FlushInstance(partyId, network);
    }

    private void FlushSide(
        MapEventSide side,
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
