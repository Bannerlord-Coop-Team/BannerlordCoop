#if DEBUG
using Common.Util;
using GameInterface.Services.PartyVisuals.Commands;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.PartyVisuals;

public class PartyVisualDebugCommandsTests
{
    [Fact]
    public void GetFixturePartiesForRestore_RetainsPartyAfterRegistryRenamesStringId()
    {
        MobileParty renamedParty = ObjectHelper.SkipConstructor<MobileParty>();
        renamedParty.StringId = "Created_123";
        renamedParty.IsActive = true;

        MobileParty[] result = PartyVisualDebugCommands.GetFixturePartiesForRestore(
            new[] { renamedParty },
            new[] { renamedParty }).ToArray();
        int liveCount = PartyVisualDebugCommands.GetLiveFixturePartyCount(
            new[] { renamedParty },
            new[] { renamedParty });

        Assert.Equal(new[] { renamedParty }, result);
        Assert.Equal(1, liveCount);
    }

    [Fact]
    public void GetFixturePartiesForRestore_FindsUnretainedPartyWithFixtureId()
    {
        MobileParty fixtureParty = ObjectHelper.SkipConstructor<MobileParty>();
        fixtureParty.StringId = "issue2938_visual_fixture_1";

        MobileParty[] result = PartyVisualDebugCommands.GetFixturePartiesForRestore(
            Enumerable.Empty<MobileParty>(),
            new[] { fixtureParty }).ToArray();

        Assert.Equal(new[] { fixtureParty }, result);
    }
}
#endif
