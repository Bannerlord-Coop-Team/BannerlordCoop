using Common;
using Common.Logging;
using GameInterface.Registry.Auto;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.ObjectManager;

public interface IAttachmentIdMapper : IGameAbstraction
{
    /// <summary>Server: snapshot the ids of live-created attachments that a joining client would re-derive differently.</summary>
    AttachmentIdMap BuildServerMap();

    /// <summary>Joining client: re-key each listed attachment from its re-derived id to the server's id.</summary>
    void ApplyClientMap(AttachmentIdMap map);
}

/// <summary>
/// Reconciles a joining client's re-derived attachment ids with the server's live-create ids (see
/// <see cref="AttachmentIdMap"/>). The server build reuses each registry's RegisterAllObjects (the single source
/// of the owner-derived id formula) to emit only the entries that diverge; save-loaded attachments match on both
/// sides and are skipped, so the map only carries attachments created mid-session.
/// </summary>
internal class AttachmentIdMapper : IAttachmentIdMapper
{
    private static readonly ILogger Logger = LogManager.GetLogger<AttachmentIdMapper>();

    private readonly IObjectManager objectManager;
    private readonly IAutoRegistryFactory autoRegistryFactory;

    public AttachmentIdMapper(IObjectManager objectManager, IAutoRegistryFactory autoRegistryFactory)
    {
        this.objectManager = objectManager;
        this.autoRegistryFactory = autoRegistryFactory;
    }

    public AttachmentIdMap BuildServerMap()
    {
        var map = new Dictionary<string, string>();
        if (Campaign.Current == null) return new AttachmentIdMap(map);

        // Reuse every registry's RegisterAllObjects (the single source of the owner-derived id formula) to collect
        // the live-created attachments whose server id diverges from what a joining client re-derives.
        autoRegistryFactory.BuildIdRemap(map);

        return new AttachmentIdMap(map);
    }

    public void ApplyClientMap(AttachmentIdMap map)
    {
        if (ModInformation.IsServer) return;
        if (map?.DerivedToServerId == null) return;

        // Two-phase re-key: resolve every object by its re-derived id and remove it from that id before
        // adding any under a server id. Re-derived ids (keys) and server ids (values) share the "Created_N"
        // namespace, so one entry's server id can equal another entry's re-derived id; removing all first
        // frees those ids so an AddExisting never collides with an object that has not been re-keyed yet.
        var pending = new List<(object Instance, string ServerId)>();
        foreach (var pair in map.DerivedToServerId)
        {
            if (objectManager.TryGetObject<object>(pair.Key, out var obj))
                pending.Add((obj, pair.Value));
        }

        foreach (var entry in pending)
            objectManager.Remove(entry.Instance);

        foreach (var entry in pending)
        {
            if (!objectManager.AddExisting(entry.ServerId, entry.Instance))
                Logger.Error("Failed to re-key attachment to server id {ServerId}", entry.ServerId);
        }
    }
}
