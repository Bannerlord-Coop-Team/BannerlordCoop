using System;
using System.Threading;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Marks a puppet spawn in progress on the current thread, so the spawn and formation capture patches skip
/// another owner's replicated troop instead of re-capturing and re-broadcasting it.
/// </summary>
public interface IPuppetSpawnTracker
{
    /// <summary>True while a puppet spawn scope is open on the calling thread.</summary>
    bool IsSpawningPuppet { get; }

    /// <summary>Opens a puppet spawn scope on the calling thread; disposing it restores the previous state.</summary>
    IDisposable BeginPuppetSpawn();
}

/// <inheritdoc cref="IPuppetSpawnTracker"/>
public class PuppetSpawnTracker : IPuppetSpawnTracker, IDisposable
{
    // Per thread because the scope wraps a single synchronous Mission.SpawnAgent call on the game thread.
    private readonly ThreadLocal<bool> spawningPuppet = new ThreadLocal<bool>();

    /// <inheritdoc/>
    public bool IsSpawningPuppet => spawningPuppet.Value;

    /// <inheritdoc/>
    public IDisposable BeginPuppetSpawn() => new ThreadLocalValueScope<bool>(spawningPuppet, true);

    /// <summary>Releases the per-thread slot when the owning container is disposed.</summary>
    public void Dispose() => spawningPuppet.Dispose();
}
