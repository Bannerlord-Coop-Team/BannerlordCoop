using Xunit;

namespace GameInterface.Tests;

/// <summary>Keeps production patch registration from changing behavior in concurrent tests.</summary>
[CollectionDefinition(nameof(PatchTestCollection), DisableParallelization = true)]
public class PatchTestCollection
{
}
