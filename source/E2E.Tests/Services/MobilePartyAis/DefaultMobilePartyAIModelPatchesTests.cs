using E2E.Tests.Services.MapEvents;
using GameInterface.Services.MobilePartyAIs.Patches;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobilePartyAis;

/// <summary>
/// Verifies the stronger player party rule that stops AI parties attacking next to a stronger player.
/// </summary>
public class DefaultMobilePartyAIModelPatchesTests : MapEventTestBase
{
    private const float AttackerStrength = 100f;

    public DefaultMobilePartyAIModelPatchesTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(true, 200f, true)]
    [InlineData(true, 50f, false)]
    [InlineData(false, 200f, false)]
    public void IsStrongerPlayerParty_CountsOnlyActiveStrongerPlayerParties(bool isActive, float playerStrength, bool expected)
    {
        var playerPartyId = CreatePlayerHeroParty("player").partyId;
        var attackerPartyId = CreateServerParty();
        var enemyPartyId = CreateServerParty();
        bool? result = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(attackerPartyId, out var attackerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(enemyPartyId, out var enemyParty));

            playerParty.IsActive = isActive;
            SetEstimatedStrength(playerParty, playerStrength);
            SetEstimatedStrength(attackerParty, AttackerStrength);

            result = DefaultMobilePartyAIModelPatches.IsStrongerPlayerParty(playerParty, attackerParty, enemyParty);
        });

        Assert.Equal(expected, result);
    }

    // Stands in for a roster of that strength, since harness parties spawn with random rosters.
    private static void SetEstimatedStrength(MobileParty party, float strength)
    {
        party.Party._cachedEstimatedStrength = strength;
        party.Party._lastEstimatedStrengthVersionNo = party.Party.GetStrengthVersionNo();
    }
}
