using Xunit;

namespace GameInterface.Tests;

/// <summary>
/// Runs tests that assert on the process-wide <see cref="ContainerProvider"/> scope apart from parallel tests that clear it.
/// </summary>
[CollectionDefinition(nameof(ContainerProviderCollection), DisableParallelization = true)]
public class ContainerProviderCollection
{
    public const string Name = nameof(ContainerProviderCollection);
}
