using Xunit;

namespace GameInterface.Tests;

/// <summary>
/// Serializes tests that build view models: the TaleWorlds ViewModel constructor caches each type's
/// members in a static Dictionary without a lock, so tests that build view models at the same time can throw.
/// </summary>
[CollectionDefinition(nameof(ViewModelCollection), DisableParallelization = true)]
public class ViewModelCollection
{
    public const string Name = nameof(ViewModelCollection);
}
