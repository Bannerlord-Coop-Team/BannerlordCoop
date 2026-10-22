using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>Serializes tests that temporarily replace the process-wide mission.</summary>
[CollectionDefinition(nameof(MissionCurrentCollection), DisableParallelization = true)]
public sealed class MissionCurrentCollection { }
