namespace Missions.Battles;

/// <summary>
/// [Game thread] Whether the engine's async agent and team tick runs now, so work outside the mission tick can leave hull
/// state that tick reads for the next mission tick instead of changing it under the worker. Never waits.
/// </summary>
public interface IAsyncAgentTick
{
    /// <summary>True while the async agent tick runs; false inside the mission tick, where it never does.</summary>
    bool IsRunning { get; }
}
