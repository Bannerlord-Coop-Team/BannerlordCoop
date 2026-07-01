using GameInterface.Registry.Auto;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.ObjectManager;

public interface IAttachmentIdMapper : IGameAbstraction
{
    /// <summary>Server: snapshot the ids of live-created attachments that a joining client would re-derive differently.</summary>
    AttachmentIdMap BuildServerMap();
}

/// <summary>
/// Server side of the join-time attachment-id reconciliation (see <see cref="AttachmentIdMap"/>). Reuses each
/// registry's RegisterAllObjects (the single source of the owner-derived id formula) to emit only the entries
/// that diverge; save-loaded attachments match on both sides and are skipped, so the map only carries
/// attachments created mid-session. The joining client adopts these ids during its own RegisterAllObjects
/// (see <see cref="IAutoRegistryFactory.SetJoinIdRemap"/>).
/// </summary>
internal class AttachmentIdMapper : IAttachmentIdMapper
{
    private readonly IAutoRegistryFactory autoRegistryFactory;

    public AttachmentIdMapper(IAutoRegistryFactory autoRegistryFactory)
    {
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
}
