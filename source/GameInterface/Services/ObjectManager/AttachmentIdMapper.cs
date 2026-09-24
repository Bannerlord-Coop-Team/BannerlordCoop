using GameInterface.Registry.Auto;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IObjectManager objectManager;

    public AttachmentIdMapper(IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
    {
        this.autoRegistryFactory = autoRegistryFactory;
        this.objectManager = objectManager;
    }

    public AttachmentIdMap BuildServerMap()
    {
        var map = new Dictionary<string, string>();
        if (Campaign.Current != null)
        {
            // Reuse every registry's RegisterAllObjects (the single source of the owner-derived id formula) to collect
            // the live-created attachments whose server id diverges from what a joining client re-derives.
            autoRegistryFactory.BuildIdRemap(map);
        }

        var handles = objectManager.GetHandleMap().ToDictionary(entry => entry.Key, entry => entry.Value);
        return new AttachmentIdMap(map, handles);
    }
}
