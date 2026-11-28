using SandBox.Missions;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Hideouts;

/// <summary>
/// Sentry AI only runs on the host, so its alarms and countdown reach a peer's fail bar through the snapshot.
/// Native exposes no way to drive the counter, so this is the one place that writes its state.
/// </summary>
internal static class HideoutFailCounterSync
{
    public const float NotRunning = -1f;

    public static float Elapsed(StealthFailCounterMissionLogic counter, float missionTime)
        => counter?.IsActive == true && counter._failCounter != null
            ? missionTime - counter._failCounter.StartTime
            : NotRunning;

    public static void Apply(StealthFailCounterMissionLogic counter, NetworkHideoutState state,
        IEnumerable<Agent> agents, float missionTime)
    {
        if (counter == null) return;
        var alarmed = new HashSet<int>();
        if (state.Defenders != null)
            foreach (var defender in state.Defenders)
                if (defender.Alarmed) alarmed.Add(defender.Seed);
        counter._alarmedAgents.Clear();
        counter._alarmedAgents.AddRange(agents.Where(agent => agent.IsActive() && agent.Origin != null &&
            alarmed.Contains(agent.Origin.UniqueSeed)));

        bool running = state.Phase == HideoutPhase.Stealth && state.FailCounterElapsed >= 0f && counter._alarmedAgents.Count > 0;
        counter.IsActive = running;
        if (!running)
        {
            counter._failCounter = null;
            return;
        }
        counter._failCounter = new Timer(missionTime - state.FailCounterElapsed, counter.FailCounterSeconds, autoReset: false);
        // A new timer reads zero elapsed until its first check, which would blink the bar empty on every snapshot.
        counter._failCounter.Check(missionTime);
    }

    public static void Stop(StealthFailCounterMissionLogic counter)
    {
        if (counter != null) counter.IsActive = false;
    }
}
