using Common.Util;
using GameInterface.Services.MobilePartyAIs.Patches;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

public class MobilePartyAiLoadPatchesTests
{
    [Fact]
    public void EnsureFleeingData_MissingData_RestoresData()
    {
        var partyAi = ObjectHelper.SkipConstructor<MobilePartyAi>();

        var result = MobilePartyAiLoadPatches.EnsureFleeingData(partyAi);

        Assert.True(result);
        Assert.NotNull(partyAi._fleeingData);
    }

    [Fact]
    public void EnsureFleeingData_ExistingData_PreservesData()
    {
        var partyAi = ObjectHelper.SkipConstructor<MobilePartyAi>();
        var fleeingData = new MobilePartyAi.FleeingData();
        partyAi._fleeingData = fleeingData;

        var result = MobilePartyAiLoadPatches.EnsureFleeingData(partyAi);

        Assert.False(result);
        Assert.Same(fleeingData, partyAi._fleeingData);
    }
}
