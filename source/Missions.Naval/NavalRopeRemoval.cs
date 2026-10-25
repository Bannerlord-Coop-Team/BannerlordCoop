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
/// A leaving hull breaks only its own ropes, and no rope may hook, keep or push a hull that left (the R46 freeze). Static
/// because the rope patches call it.
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
    /// Breaks every rope and plank that starts or ends on a hull about to leave, replicas included, before vanilla's removal;
    /// the owners of the other ends see their rope gone and send its removal.
    /// </summary>
    internal static void BreakConnections(MissionShip hull) => NavalRopes.PastReplicaGates(hull.BreakAllExistingConnections);

    /// <summary>
    /// Forgets a hull that left on every removal path: its stations' records, and every station's AI preference for it, which
    /// vanilla's removal keeps and its pilot AI throws at (GetBestEnemyAttachment) without a removal check.
    /// </summary>
    internal static void ForgetRemovedHull(IEnumerable<MissionShip> ships, MissionShip removed)
    {
        NavalRopes.Forget(removed);

        foreach (var ship in ships)
        {
            if (ship == removed) continue;

            foreach (var machine in ship.AttachmentMachines)
            {
                if (machine.GetPreferredTargetShip() == removed) machine.SetPreferredTargetShip(null);
            }
        }
    }

    /// <summary>
    /// Whether vanilla's removal of <paramref name="removed"/> breaks this rope. In a coop naval battle only a rope with an end
    /// on that hull breaks, because hulls leave mid-battle there and vanilla's removal would end every other boarding.
    /// </summary>
    internal static bool BreaksOnRemoval(Attachment rope, MissionShip removed) =>
        !CoopNavalMissionScope.IsActive || rope.AttachmentSource?.OwnerShip == removed || rope.AttachmentTarget?.OwnerShip == removed;

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
