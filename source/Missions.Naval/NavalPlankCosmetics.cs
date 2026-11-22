using GameInterface;
using System;
using System.Globalization;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;

namespace Missions.Naval;

/// <summary>
/// Plank decoration a replica draws like its owner: a seed every client derives alike, and the owner's plank count.
/// </summary>
internal static class NavalPlankCosmetics
{
    // A seeded random for one plank cosmetics step, or null outside a coop naval battle so vanilla's random is used.
    internal static Random PlankCosmeticRandom(Attachment attachment, string purpose)
    {
        var source = attachment?.AttachmentSource;
        if (!NavalMissionTracker.IsActiveHere() || source?.OwnerShip == null) return null;
        if (!ContainerProvider.TryResolve<INavalRopeRegistry>(out var ropes)) return null;

        var record = ropes.RecordOf(source);
        if (purpose == "AddRopesToBridge" && !ropes.IsReplica(attachment) && record != null)
            record.DecorationPlanks = attachment._numberOfPlanksNeeded;

        long generation = record?.Generation ?? 0;
        if (!ropes.BypassReplicaGates && !ReferenceEquals(record?.Attachment, attachment)) generation++;
        string key = NavalShipEngine.EntityPath(source.GameEntity, source.OwnerShip.GameEntity) + ":"
            + generation.ToString(CultureInfo.InvariantCulture) + ":" + purpose;
        int seed = 17;
        unchecked
        {
            foreach (char value in key) seed = (seed * 31) + value;
        }

        return new Random(seed);
    }

    // The plank count a decoration loop lays: the owner's for a replica it has sampled, else vanilla's own.
    internal static int DecorationCount(Attachment attachment)
    {
        var record = ContainerProvider.TryResolve<INavalRopeRegistry>(out var ropes) && ropes.IsReplica(attachment)
            ? ropes.RecordOf(attachment.AttachmentSource)
            : null;
        return record != null && record.DecorationPlanks > 0 ? record.DecorationPlanks : attachment._numberOfPlanksNeeded;
    }
}
