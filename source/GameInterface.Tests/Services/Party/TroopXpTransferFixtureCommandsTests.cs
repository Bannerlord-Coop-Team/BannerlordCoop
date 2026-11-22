using Common.Commands;
using Common.Util;
using GameInterface.Services.Party.Commands;
using GameInterface.Utils.Commands;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Party;

// Covers argument resolution in the clan-party XP transfer fixture commands.
public class TroopXpTransferFixtureCommandsTests
{
    [Fact]
    public void FixtureState_AmbiguousClanPartyName_FailsInsteadOfReportingMissingParty()
    {
        var playerParty = ObjectHelper.SkipConstructor<MobileParty>();
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        MobileParty noParty = null;
        string noError = null;
        string ambiguous = "Several Mobile parties are named 'Twin'; use an id. Candidates: Twin (p1), Twin (p2)";
        var resolver = new Mock<ICommandObjectResolver>();
        resolver.Setup(r => r.TryResolveMobileParty("player", out playerParty, out noError)).Returns(true);
        resolver.Setup(r => r.TryResolveCharacterObject("troop", out character, out noError)).Returns(true);
        resolver.Setup(r => r.TryResolveMobileParty("Twin", out noParty, out ambiguous)).Returns(false);
        resolver.Setup(r => r.IsAmbiguous(ambiguous)).Returns(true);

        var result = new TroopXpTransferFixtureCommands.ClanPartyXpFixtureStateCoopCommand(resolver.Object)
            .ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { "player", "Twin", "troop" }));

        Assert.False(result.Succeeded);
        Assert.Equal(ambiguous, result.Output);
    }
}
