#if DEBUG
using System;
using Common.Logging;
using Serilog;
using Missions.Messages;

namespace Missions.Battles;

public sealed partial class NavalLabController
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalLabController>();
    private string terminalReason;
    private long terminalCallback;
    private string controlCleanupFailure;
    private string holdFailure;
    private bool sceneReady;
    private bool materializeAttempted;
    private volatile bool hydrated;
    private volatile bool terminal;
    private string assignedHost;
    private double deadline;

    private bool AssignmentValid => !disposed && !terminal && session.HostEpoch == 1
        && OriginalOwnersReady && session.HostControllerId == assignedHost;

    // Materializes the fixture once both owners are present; returns whether released ticks may run.
    private bool TickLifecycle()
    {
        if (terminal) return false;
        try
        {
            if (adapter.Blocker != null) throw new InvalidOperationException(adapter.Blocker);
            if (Now >= deadline) throw new InvalidOperationException("factory_probe.deadline");
            if (session.HostEpoch > 1 || (materializeAttempted && !AssignmentValid))
                throw new InvalidOperationException("factory_probe.assignment_changed");
            if (!sceneReady || !OriginalOwnersReady || session.HostEpoch != 1) return false;
            if (!materializeAttempted)
            {
                materializeAttempted = true;
                assignedHost = session.HostControllerId;
                adapter.MaterializeFactoryProbe(session.IsLocalHost, () => AssignmentValid);
                if (!AssignmentValid || adapter.Blocker != null
                    || adapter.Agents.Length != manifest.Combatants.Length || Array.Exists(adapter.Agents, agent => agent == null))
                    throw new InvalidOperationException(adapter.Blocker ?? "factory_probe.incomplete_or_assignment_changed");
                RegisterFixtureAgents();
                hydrated = true;
                relay.SendAll(new NetworkNavalLabReceipt(manifest.IncarnationId, manifest.IncarnationId, "hydrated"));
            }
            return released;
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
            return false;
        }
    }

    public object TerminalStatus() => new
    {
        terminal, reason = terminalReason, localCallback = terminalCallback,
        adapterBlocker = adapter?.Blocker, faultReported, controlCleanupFailure, holdFailure
    };

    private void Hold(string reason)
    {
        if (!terminal)
        {
            terminalReason = reason;
            terminalCallback = callback;
        }
        terminal = true;
        released = false;
        try { CancelControls(); }
        catch (Exception exception)
        {
            controlCleanupFailure = controlCleanupFailure ?? exception.ToString();
            Logger.Error(exception, "[NavalLabTerminal] {Reason} control cleanup failed", reason);
        }
        finally
        {
            // Native control failure must not bypass the complete-body hold attempt.
            try { adapter?.Hold(); }
            catch (Exception exception)
            {
                holdFailure = holdFailure ?? exception.ToString();
                Logger.Error(exception, "[NavalLabTerminal] {Reason} body hold failed; process exit required", reason);
            }
        }
    }

    private void Fail(string reason)
    {
        try { Hold(reason); }
        finally
        {
            if (!faultReported)
            {
                faultReported = true;
                Logger.Error("[NavalLabTerminal] {Incarnation} local fault: {Reason}", manifest.IncarnationId, reason);
                relay.SendAll(new NetworkNavalLabFault(manifest.IncarnationId, reason));
            }
        }
    }
}
#endif
