using Xunit;

namespace GameInterface.Tests;

/// <summary>
/// Serializes tests that mutate the process-wide BattleSpawnGate battle state.
/// </summary>
[CollectionDefinition(nameof(BattleSpawnGateCollection), DisableParallelization = true)]
public class BattleSpawnGateCollection
{
    public const string Name = nameof(BattleSpawnGateCollection);
}
