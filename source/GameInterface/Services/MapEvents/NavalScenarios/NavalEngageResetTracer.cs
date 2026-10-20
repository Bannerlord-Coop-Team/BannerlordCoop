#if DEBUG
using Common.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.NavalScenarios;

/// <summary>
/// [Game thread, server] DEBUG: logs each distinct call stack that resets the encounter preset's engaging fleet's movement
/// while its engage step waits, to name what replaces the engage order (R41b P4).
/// </summary>
public interface INavalEngageResetTracer
{
    /// <summary>Starts tracing <paramref name="party"/>, replacing any party traced before.</summary>
    void Watch(MobileParty party);

    /// <summary>Stops tracing <paramref name="party"/>; ignored when another party is traced.</summary>
    void Stop(MobileParty party);

    /// <summary>Logs the caller's stack once per distinct stack when <paramref name="party"/> is the traced one.</summary>
    void OnMovementReset(MobileParty party);
}

/// <inheritdoc cref="INavalEngageResetTracer"/>
public class NavalEngageResetTracer : INavalEngageResetTracer
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalEngageResetTracer>();

    private readonly HashSet<string> loggedStacks = new HashSet<string>(StringComparer.Ordinal);
    private MobileParty watched;

    public void Watch(MobileParty party)
    {
        watched = party;
        loggedStacks.Clear();
    }

    public void Stop(MobileParty party)
    {
        if (watched != party) return;

        watched = null;
        loggedStacks.Clear();
    }

    public void OnMovementReset(MobileParty party)
    {
        if (watched == null || party != watched) return;

        string stack = Environment.StackTrace;
        if (!loggedStacks.Add(stack)) return;

        Logger.Information("[NavalScenario] Encounter fleet {Party} movement reset (behavior {Behavior}, target {Target}) by {Stack}",
            party.StringId, party.DefaultBehavior, party.TargetParty?.StringId ?? "none", stack);
    }
}
#endif
