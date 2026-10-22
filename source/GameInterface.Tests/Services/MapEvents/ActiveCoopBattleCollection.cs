using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>
/// Runs tests that install an <see cref="ActiveCoopBattleScope"/> apart from every parallel test, because the scope
/// swaps the process-wide container that other tests set and clear.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ActiveCoopBattleCollection
{
    public const string Name = nameof(ActiveCoopBattleCollection);
}
