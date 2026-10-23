using GameInterface.Services.Clans.Patches;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

/// <summary>
/// Covers the party wage prefix falling back to vanilla when no coop container is active.
/// </summary>
[Collection(ContainerProviderCollection.Name)]
public class DefaultPartyWageModelPatchesTests
{
    /// <summary>
    /// A single-player load after a coop session has no container, so the prefix must let vanilla compute wages.
    /// </summary>
    [Fact]
    public void GetTotalWagePrefix_NoContainer_RunsVanilla()
    {
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            ContainerProvider.Clear();
            var result = new ExplainedNumber();

            bool runOriginal = DefaultPartyWageModelPatches.GetTotalWagePrefix(null, ref result, null, null);

            Assert.True(runOriginal);
        }
        finally
        {
            if (hadPreviousContainer) ContainerProvider.SetContainer(previousContainer);
            else ContainerProvider.Clear();
        }
    }
}
