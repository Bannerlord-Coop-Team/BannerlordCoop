using Xunit;

namespace GameInterface.Tests;

/// <summary>Serializes tests that share the process-wide mission and battle-spawn state.</summary>
[CollectionDefinition(nameof(MissionCurrentCollection), DisableParallelization = true)]
public sealed class MissionCurrentCollection { }
