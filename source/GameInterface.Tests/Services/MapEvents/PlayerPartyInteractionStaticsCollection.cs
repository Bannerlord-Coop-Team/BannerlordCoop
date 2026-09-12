using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>
/// Serialises the test classes that mutate the process-wide PlayerPartyInteractionDialogState statics so xUnit
/// does not run them against each other in parallel.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PlayerPartyInteractionStaticsCollection
{
    public const string Name = "PlayerPartyInteraction statics";
}
