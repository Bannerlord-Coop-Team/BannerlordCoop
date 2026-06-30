using Common;
using Common.Logging;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

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
/// <see cref="AttachmentIdMap"/>). The server build iterates the same owners RegisterAllObjects does and
/// mirrors each registry's id formula, emitting only the entries that diverge; save-loaded attachments
/// match on both sides and are skipped, so the map only carries attachments created mid-session.
/// </summary>
internal class AttachmentIdMapper : IAttachmentIdMapper
{
    private static readonly ILogger Logger = LogManager.GetLogger<AttachmentIdMapper>();

    private readonly IObjectManager objectManager;

    public AttachmentIdMapper(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public AttachmentIdMap BuildServerMap()
    {
        var map = new Dictionary<string, string>();
        if (Campaign.Current == null) return new AttachmentIdMap(map);

        foreach (var party in MobileParty.All)
        {
            if (party == null) continue;

            // Mirror each registry's RegisterAllObjects id formula; keep these in sync with PartyBaseRegistry /
            // TroopRosterRegistry / ItemRosterRegistry / PartyComponentRegistry / MobilePartyAiRegistry.
            AddIfDiverged(map, party.Party, "PartyBase_" + party.StringId);
            AddIfDiverged(map, party.MemberRoster, "TroopRoster_MemberRoster_" + party.StringId);
            AddIfDiverged(map, party.PrisonRoster, "TroopRoster_PrisonRoster_" + party.StringId);
            AddIfDiverged(map, party.ItemRoster, "ItemRoster_" + party.StringId);
            AddIfDiverged(map, party.PartyComponent, "PartyComponent_" + party.StringId);
            AddIfDiverged(map, party.Ai, "MobilePartyAi_" + party.StringId);
        }

        var mapEvents = Campaign.Current.MapEventManager?.MapEvents;
        if (mapEvents != null)
        {
            foreach (var mapEvent in mapEvents)
            {
                // Mirror MapEventSideRegistry: a positional counter over the non-null sides.
                int counter = 1;
                foreach (var side in mapEvent._sides.Where(side => side != null))
                    AddIfDiverged(map, side, "MapEventSide_" + mapEvent.StringId + "_" + counter++);
            }
        }

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

    private void AddIfDiverged(Dictionary<string, string> map, object attachment, string derivedId)
    {
        if (attachment == null) return;
        if (!objectManager.TryGetId(attachment, out var serverId)) return;
        if (serverId == derivedId) return;

        map[derivedId] = serverId;
    }
}
