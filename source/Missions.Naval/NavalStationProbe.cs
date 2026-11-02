#if DEBUG
using Common.Commands;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Routes validated station diagnostics to the local naval mission.</summary>
public interface INavalStationProbe
{
    /// <summary>Reads stations, starts a timed watch, or re-seats one authoritative agent.</summary>
    CoopCommandResult Execute(string mode, string hullPrefix, int value);
}

/// <summary>Finds the mission-owned probe without retaining a mission across battles.</summary>
public sealed class NavalStationProbe : INavalStationProbe
{
    /// <summary>Forwards validated commands to the current co-op naval mission.</summary>
    public CoopCommandResult Execute(string mode, string hullPrefix, int value)
    {
        var probe = Mission.Current?.GetMissionBehavior<NavalStationProbeLogic>();
        return probe == null
            ? new CoopCommandResult(false, "No active coop naval battle mission.", "command_failed")
            : probe.Execute(mode, hullPrefix, value);
    }
}
#endif
