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
/// Verifies a player clan changing kingdom holds the nearby lords that chased its player party, like vanilla
/// does around the main party.
/// </summary>
public class AiPartyThinkBehaviorPatchesTests : MapEventTestBase
{
    public AiPartyThinkBehaviorPatchesTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ClanChangedKingdom_HoldsOnlyLordsNoLongerAtWarThatChaseItsPlayerParty()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var (_, otherPlayerPartyId) = CreatePlayerHeroParty("PlayerTwo");
        var peacefulLordId = CreateServerParty();
        var warLordId = CreateServerParty();
        var otherChaserId = CreateServerParty();

        Server.Call(() =>
        {
            AddEncounterModel();
            var playerParty = Server.GetRegisteredObject<MobileParty>(playerPartyId);
            var otherPlayerParty = Server.GetRegisteredObject<MobileParty>(otherPlayerPartyId);
            var peacefulLord = Server.GetRegisteredObject<MobileParty>(peacefulLordId);
            var warLord = Server.GetRegisteredObject<MobileParty>(warLordId);
            var otherChaser = Server.GetRegisteredObject<MobileParty>(otherChaserId);
            Assert.Contains(playerParty.WarPartyComponent, playerParty.ActualClan.WarPartyComponents);

            PlaceNear(playerParty, otherPlayerParty, 1f);
            PlaceNear(playerParty, peacefulLord, 2f);
            PlaceNear(playerParty, warLord, 3f);
            PlaceNear(playerParty, otherChaser, 4f);
            Chase(peacefulLord, playerParty);
            Chase(warLord, playerParty);
            Chase(otherChaser, otherPlayerParty);
            FactionManager.DeclareWar(warLord.MapFaction, playerParty.MapFaction);

            new AiPartyThinkBehavior().OnClanChangedKingdom(
                playerParty.ActualClan, null, null, ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);

            Assert.Equal(AiBehavior.Hold, peacefulLord.DefaultBehavior);
            Assert.Equal(AiBehavior.EngageParty, warLord.DefaultBehavior);
            Assert.Equal(AiBehavior.EngageParty, otherChaser.DefaultBehavior);
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
