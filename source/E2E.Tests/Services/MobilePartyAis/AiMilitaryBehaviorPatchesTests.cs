using E2E.Tests.Services.MapEvents;
using GameInterface.Services.MobilePartyAIs.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobilePartyAis;

/// <summary>
/// Verifies the server AI skips a settlement whose last attacker it is barred from attacking, the co-op
/// version of vanilla's <c>DoNotAttackMainPartyUntil</c> check.
/// </summary>
public class AiMilitaryBehaviorPatchesTests : MapEventTestBase
{
    public AiMilitaryBehaviorPatchesTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(true, true, false)] // protected, player attacked last: skipped
    [InlineData(false, true, true)] // not protected, player attacked last: suitable
    [InlineData(true, false, true)] // protected, another party attacked last: suitable
    public void Settlement_IsSkippedOnlyWhileProtectedFromItsLastAttacker(bool isProtected, bool playerAttackedLast, bool expectedSuitable)
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var aiPartyId = CreateServerParty();
        var otherAttackerId = CreateServerParty();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        bool? suitable = null;

        Server.Call(() =>
        {
            var playerParty = Server.GetRegisteredObject<MobileParty>(playerPartyId);
            var aiParty = Server.GetRegisteredObject<MobileParty>(aiPartyId);
            var otherAttacker = Server.GetRegisteredObject<MobileParty>(otherAttackerId);
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            settlement.LastAttackerParty = playerAttackedLast ? playerParty : otherAttacker;

            try
            {
                // The test bootstrap stubs HoursFromNow to Zero, so build the deadline from the current time.
                if (isProtected)
                    DefaultMobilePartyAIModelPatches.PreventAttacksUntil(aiParty, playerParty, Campaign.Current.MapTimeTracker.Now + CampaignTime.Hours(12));

                suitable = new AiMilitaryBehavior().CheckIfSettlementIsSuitableForMilitaryAction(
                    settlement, aiParty, Army.ArmyTypes.Besieger, isCalculatingForNewArmyCreation: false);
            }
            finally
            {
                DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections();
            }
        }, MapEventDisabledMethods);

        Assert.Equal(expectedSuitable, suitable);
    }
}
