using Common.Logging;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using Serilog;
using System.Collections.Generic;
using System.Threading;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;
using Joint = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachmentJoint;
using RopeState = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment.ShipAttachmentState;

namespace Missions.Naval;

/// <summary>
/// No rope may hook, keep or push a hull that left the mission (the R46 freeze). Static because the rope patches call it.
/// </summary>
internal static class NavalRopeRemoval
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(NavalRopeRemoval));

    private static long jointStepsSkipped;

    /// <summary>[Any thread] Joint force steps skipped this mission because the joint lacked a hull.</summary>
    internal static long JointStepsSkipped => Interlocked.Read(ref jointStepsSkipped);

    /// <summary>Resets the skip counter at mission end.</summary>
    internal static void Clear() => Interlocked.Exchange(ref jointStepsSkipped, 0);

    /// <summary>
    /// Breaks every rope and plank that starts or ends on a hull leaving the mission, replicas included, and forgets its
    /// stations; the owners of the other ends see their rope gone and send its removal.
    /// </summary>
    internal static void BreakConnections(MissionShip hull)
    {
        NavalRopes.PastReplicaGates(hull.BreakAllExistingConnections);
        NavalRopes.Forget(hull);
    }

    /// <summary>
    /// Drops every station's AI preference for a hull that left the mission. Vanilla's removal clears the boarding order but
    /// not these, and its pilot AI throws at the preferred hull's points (GetBestEnemyAttachment) without a removal check.
    /// </summary>
    internal static void ForgetTarget(IEnumerable<MissionShip> ships, MissionShip removed)
    {
        foreach (var ship in ships)
        {
            if (ship == removed) continue;

            foreach (var machine in ship.AttachmentMachines)
            {
                if (machine.GetPreferredTargetShip() == removed) machine.SetPreferredTargetShip(null);
            }
        }
    }

    /// <summary>Whether a rope end has no hull: it left the mission, or never resolved to one (a joint built against a removed hull).</summary>
    internal static bool IsMissingOrRemoved(MissionShip hull) => hull == null || hull.IsRemoved;

    /// <summary>Breaks a rope whose end left; the machine tick destroys it right after this attachment's tick.</summary>
    internal static bool BreakIfEndGone(Attachment attachment)
    {
        if (!EndIsGone(attachment)) return false;

        NavalRopes.PastReplicaGates(() => attachment.SetAttachmentState(RopeState.BrokenAndWaitingForRemoval));
        return true;
    }

    /// <summary>
    /// Whether a live rope lost an end: its source or target hull left the mission, or its joint was built without one (the
    /// joint guard skips such a joint's step, so only breaking the rope ends it).
    /// </summary>
    internal static bool EndIsGone(Attachment attachment)
    {
        if (attachment == null || attachment.State == RopeState.BrokenAndWaitingForRemoval) return false;
        if (attachment.AttachmentSource?.OwnerShip?.IsRemoved == true || attachment.AttachmentTarget?.OwnerShip?.IsRemoved == true) return true;

        var joint = attachment.ShipAttachmentJoint;
        return joint != null && !JointHasHulls(joint);
    }

    /// <summary>
    /// [Fixed-tick thread] Runs a joint's step only while it has both hulls; the first skip in a mission is logged, since the
    /// rope tick should break such a rope at once.
    /// </summary>
    internal static bool AllowJointStep(Joint joint)
    {
        if (JointHasHulls(joint)) return true;

        if (Interlocked.Increment(ref jointStepsSkipped) == 1)
            Logger.Warning("[NavalSync] Skipped the force step of a rope joint missing a hull; its rope breaks on the next rope tick");
        return false;
    }

    /// <summary>
    /// [Fixed-tick thread] Whether a joint's two hulls are still in the mission. A joint built after its target hull left
    /// holds no target hull at all, and every one of its force steps dereferences both, so it skips the step instead.
    /// </summary>
    internal static bool JointHasHulls(Joint joint) =>
        joint != null && !IsMissingOrRemoved(joint._shipSourceScript) && !IsMissingOrRemoved(joint._shipTargetScript);
}
