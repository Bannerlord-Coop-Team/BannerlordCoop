using E2E.Tests.Services.MapEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobilePartyAis;

/// <summary>
/// Verifies a player clan changing kingdom holds the nearby lords that chased its player parties, like vanilla
/// does around the main party.
/// </summary>
public class AiPartyThinkBehaviorPatchesTests : MapEventTestBase
{
    public AiPartyThinkBehaviorPatchesTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ClanChangedKingdom_HoldsOnlyLordsNoLongerAtWarThatChaseItsPlayerParties()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var (_, clanmatePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var aiPartyId = CreateServerParty();
        var peacefulLordId = CreateServerParty();
        var clanmateChaserId = CreateServerParty();
        var warLordId = CreateServerParty();
        var aiPartyChaserId = CreateServerParty();

        Server.Call(() =>
        {
            AddEncounterModel();
            var playerParty = Server.GetRegisteredObject<MobileParty>(playerPartyId);
            var clanmateParty = Server.GetRegisteredObject<MobileParty>(clanmatePartyId);
            var aiParty = Server.GetRegisteredObject<MobileParty>(aiPartyId);
            var peacefulLord = Server.GetRegisteredObject<MobileParty>(peacefulLordId);
            var clanmateChaser = Server.GetRegisteredObject<MobileParty>(clanmateChaserId);
            var warLord = Server.GetRegisteredObject<MobileParty>(warLordId);
            var aiPartyChaser = Server.GetRegisteredObject<MobileParty>(aiPartyChaserId);
            var playerClan = playerParty.ActualClan;

            // The harness gives every party its own clan, so move the second player and an AI party into the
            // first player's clan, which then holds player and non-player war parties.
            clanmateParty.ActualClan = playerClan;
            aiParty.ActualClan = playerClan;
            Assert.Contains(playerParty.WarPartyComponent, playerClan.WarPartyComponents);
            Assert.Contains(clanmateParty.WarPartyComponent, playerClan.WarPartyComponents);
            Assert.Contains(aiParty.WarPartyComponent, playerClan.WarPartyComponents);

            PlaceNear(playerParty, clanmateParty, 1f);
            PlaceNear(playerParty, aiParty, 1.5f);
            PlaceNear(playerParty, peacefulLord, 2f);
            PlaceNear(playerParty, clanmateChaser, 2.5f);
            PlaceNear(playerParty, warLord, 3f);
            PlaceNear(playerParty, aiPartyChaser, 3.5f);
            Chase(peacefulLord, playerParty);
            Chase(clanmateChaser, clanmateParty);
            Chase(warLord, playerParty);
            Chase(aiPartyChaser, aiParty);
            FactionManager.DeclareWar(warLord.MapFaction, playerClan.MapFaction);

            new AiPartyThinkBehavior().OnClanChangedKingdom(
                playerClan,
                oldKingdom: null,
                newKingdom: null,
                ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom,
                showNotification: false);

            Assert.Equal(AiBehavior.Hold, peacefulLord.DefaultBehavior);
            Assert.Equal(AiBehavior.Hold, clanmateChaser.DefaultBehavior);
            Assert.Equal(AiBehavior.EngageParty, warLord.DefaultBehavior);
            Assert.Equal(AiBehavior.EngageParty, aiPartyChaser.DefaultBehavior);
        }, MapEventDisabledMethods);
    }

    // The harness campaign has no encounter model, which the hold reads for its search radius.
    private static void AddEncounterModel()
    {
        if (Campaign.Current.Models.EncounterModel != null) return;

        var models = Campaign.Current.Models.GetGameModels().ToList();
        models.Add(new DefaultEncounterModel());
        var gameModels = new GameModels(models);
        Game.Current._gameModelManagers[typeof(GameModels)] = gameModels;
        Campaign.Current._gameModels = gameModels;
    }

    // Moves a party onto the map next to another party, inside the hold's search radius.
    private static void PlaceNear(MobileParty anchor, MobileParty party, float offset)
    {
        party.Position = new CampaignVec2(anchor.Position.ToVec2() + new Vec2(offset, 0f), true);
    }

    // Orders a lord to engage a party, with the short term target the hold reads.
    private static void Chase(MobileParty lord, MobileParty target)
    {
        lord.SetMoveEngageParty(target, MobileParty.NavigationType.Default);
        lord.RecalculateShortTermBehavior();
        Assert.Same(target, lord.ShortTermTargetParty);
        Assert.Equal(AiBehavior.EngageParty, lord.DefaultBehavior);
    }
}
