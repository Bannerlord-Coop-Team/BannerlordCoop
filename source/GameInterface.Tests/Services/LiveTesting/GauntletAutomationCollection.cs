using Xunit;

namespace GameInterface.Tests.Services.LiveTesting;

/// <summary>
/// Runs tests that use the static <see cref="TaleWorlds.ScreenSystem.ScreenManager"/> screens one at a time.
/// Not guarded by DEBUG so tests that also run in Release can join it.
/// </summary>
[CollectionDefinition(nameof(GauntletAutomationCollection), DisableParallelization = true)]
public sealed class GauntletAutomationCollection { }
