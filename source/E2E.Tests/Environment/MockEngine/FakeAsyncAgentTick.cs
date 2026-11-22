using Missions.Battles;

namespace E2E.Tests.Environment.MockEngine;

/// <summary>
/// One client's stand-in for the engine's async agent tick, which never runs headless; a scenario sets
/// <see cref="IsRunning"/> to act as if work outside the mission tick ran while that tick does.
/// </summary>
public sealed class FakeAsyncAgentTick : IAsyncAgentTick
{
    /// <summary>Whether the async agent tick runs now; false unless the scenario acts it out.</summary>
    public bool IsRunning { get; set; }
}
