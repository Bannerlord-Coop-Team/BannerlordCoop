#if DEBUG
using Common;
using Common.Commands;
using Common.Util;
using Moq;
using System;
using GameInterface.Services.PartyVisuals.Commands;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.PartyVisuals;

[Collection(ModInformationRoleCollection.Name)]
public class PartyVisualDebugCommandsTests
{
    [Fact]
    public void PreparePlayer_DelegatesToProductionUnstuckWithTheOriginalArguments()
    {
        var previousRole = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = true;
            var args = new CoopCommandArgsFactory().FromValues(new[] { "testclient" });
            var result = new CoopCommandResult(true, "production recovery");
            var registry = new Mock<ICoopCommandRegistry>();
            registry.Setup(value => value.ProcessCommand("coop.unstuck", args)).Returns(result);
            var command = new PartyVisualDebugCommands.PreparePlayerCoopCommand(
                new Lazy<ICoopCommandRegistry>(() => registry.Object));

            Assert.Same(result, command.ProcessCommand(args));
            registry.Verify(value => value.ProcessCommand("coop.unstuck", args), Times.Once);
            Assert.Equal(CoopCommandSide.Server, command.Side);
        }
        finally
        {
            ModInformation.IsServer = previousRole;
        }
    }

    [Fact]
    public void PreparePlayer_OnClientDoesNotResolveTheProductionRegistry()
    {
        var previousRole = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = false;
            var command = new PartyVisualDebugCommands.PreparePlayerCoopCommand(
                new Lazy<ICoopCommandRegistry>(() => throw new InvalidOperationException("unexpected resolution")));

            Assert.False(command.ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { "testclient" })).Succeeded);
        }
        finally
        {
            ModInformation.IsServer = previousRole;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CountPartyVisuals_CountsOnlyTheSelectedPartyIncludingDuplicates(int expectedCount)
    {
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        var otherParty = ObjectHelper.SkipConstructor<PartyBase>();
        var visualParties = Enumerable.Repeat(party, expectedCount).Concat(new[] { otherParty });

        Assert.Equal(expectedCount, PartyVisualDebugCommands.CountPartyVisuals(visualParties, party));
    }

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
