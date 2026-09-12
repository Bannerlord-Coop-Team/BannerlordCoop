using GameInterface.Services.BesiegerCamps.Patches;
using GameInterface.Services.SiegeEvents;
using Xunit;

namespace GameInterface.Tests.Services.BesiegerCamps;

[Collection(ModInformationRoleCollection.Name)]
public class BesiegerCampAssaultPatchesTests
{
    [Fact]
    public void NoContainer_UsesDedicatedHostSafeReadiness()
    {
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        try
        {
            ContainerProvider.Clear();

            Assert.IsType<AiSiegeAssaultReadiness>(BesiegerCampAssaultPatches.ResolveReadiness());
        }
        finally
        {
            if (hadPreviousContainer)
            {
                ContainerProvider.SetContainer(previousContainer);
            }
            else
            {
                ContainerProvider.Clear();
            }
        }
    }
}
