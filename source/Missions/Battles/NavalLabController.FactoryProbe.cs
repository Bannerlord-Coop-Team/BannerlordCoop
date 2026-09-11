#if DEBUG
using System;
using Common.Logging;
using Serilog;
using Missions.Messages;

namespace Missions.Battles;

public sealed partial class NavalLabController
{
    private static readonly ILogger FactoryProbeLogger = LogManager.GetLogger<NavalLabController>();
    private string factoryControlCleanupFailure;
    private string factoryHoldFailure;
    private bool IsFactoryProbe => manifest?.Mode == NavalLabMode.FactoryAuthorityProbe || IsTwoClientNative;
    private bool factorySceneReady;
    private bool factoryAttempted;
    private volatile bool factoryHydrated;
    private volatile bool factoryTerminal;
    private string factoryHost;
    private double factoryDeadline;

    private bool FactoryAssignmentValid => !disposed && !factoryTerminal && session.HostEpoch == 1
        && OriginalOwnersReady && session.HostControllerId == factoryHost;

    private bool TickFactoryProbe()
    {
        if (factoryTerminal) return false;
        try
        {
            if (adapter.Blocker != null) throw new InvalidOperationException(adapter.Blocker);
            if (Now >= factoryDeadline) throw new InvalidOperationException("factory_probe.deadline");
            if (session.HostEpoch > 1 || (factoryAttempted && !FactoryAssignmentValid))
                throw new InvalidOperationException("factory_probe.assignment_changed");
            if (!factorySceneReady || !OriginalOwnersReady || session.HostEpoch != 1) return false;
            if (!factoryAttempted)
            {
                factoryAttempted = true;
                factoryHost = session.HostControllerId;
                adapter.MaterializeFactoryProbe(session.IsLocalHost, () => FactoryAssignmentValid);
                if (!FactoryAssignmentValid || adapter.Blocker != null
                    || adapter.Agents.Length != manifest.Combatants.Length || Array.Exists(adapter.Agents, agent => agent == null))
                    throw new InvalidOperationException(adapter.Blocker ?? "factory_probe.incomplete_or_assignment_changed");
                RegisterFixtureAgents();
                factoryHydrated = true;
                relay.SendAll(new NetworkNavalLabReceipt(manifest.IncarnationId, manifest.IncarnationId, "hydrated"));
            }
            // An anchored elected body may already integrate; hydration is only a messaging/input gate.
            return released;
        }
        catch (Exception exception)
        {
            FailFactoryProbe(exception.ToString());
            return false;
        }
    }

    private void HoldFactoryProbe(string reason)
    {
        factoryTerminal = true;
        released = false;
        try { CancelControls(reason); }
        catch (Exception exception)
        {
            factoryControlCleanupFailure = factoryControlCleanupFailure ?? exception.ToString();
            FactoryProbeLogger.Error(exception, "[NavalLabTerminal] {Reason} control cleanup failed", reason);
        }
        finally
        {
            // Native control failure must not bypass the complete-body hold attempt.
            try { HoldAdapter(); }
            catch (Exception exception)
            {
                factoryHoldFailure = factoryHoldFailure ?? exception.ToString();
                FactoryProbeLogger.Error(exception, "[NavalLabTerminal] {Reason} body hold failed; process exit required", reason);
            }
        }
        CompletePending("cancelled:" + reason);
    }

    private void FailFactoryProbe(string reason)
    {
        try { HoldFactoryProbe(reason); }
        finally
        {
            if (!faultReported)
            {
                faultReported = true;
                relay.SendAll(new NetworkNavalLabFault(manifest.IncarnationId, reason));
            }
        }
    }
}
#endif
