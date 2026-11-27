using E2E.Tests.Environment;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements;

public class GarrisonArmyTransferTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public GarrisonArmyTransferTests(ITestOutputHelper output) => environment = new E2ETestEnvironment(output);
    public void Dispose() => environment.Dispose();

    [Fact]
    public void ArmyGarrisonTransfer_LeavesAttachedPlayerPartyOut()
    {
        var armyId = environment.CreateRegisteredObject<Army>();
        var aiPartyId = environment.CreateRegisteredObject<MobileParty>();
        var playerPartyId = environment.CreateRegisteredObject<MobileParty>();
        var server = environment.Server;

        server.Call(() =>
        {
            var army = server.GetRegisteredObject<Army>(armyId);
            var aiParty = server.GetRegisteredObject<MobileParty>(aiPartyId);
            var playerParty = server.GetRegisteredObject<MobileParty>(playerPartyId);
            Assert.True(server.ObjectManager.TryGetId(playerParty.LeaderHero, out var heroId));
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("Joiner", heroId, playerPartyId, null, null)));
            Assert.True(playerParty.IsPlayerParty());

            foreach (var member in new[] { aiParty, playerParty })
            {
                member.Army = army;
                army.AddPartyToMergedParties(member);
            }

            UseGarrisonTransferModels();

            // The parties an AI army moves troops between and the garrison when its leader enters a friendly fortification
            var picked = new GarrisonTroopsCampaignBehavior().CalculateMobilePartiesIdealPartySizes(army.LeaderParty)
                .Select(entry => server.ObjectManager.TryGetId(entry.Item1, out var id) ? id : null)
                .ToList();

            Assert.Contains(aiPartyId, picked);
            Assert.DoesNotContain(playerPartyId, picked);
        });
    }

    // The harness Campaign boots without these, and sizing an army party reads its member limit and food use
    private void UseGarrisonTransferModels()
    {
        var models = (Campaign.Current.Models?.GetGameModels() ?? Enumerable.Empty<GameModel>())
            .Where(model => model is not PartySizeLimitModel && model is not MobilePartyFoodConsumptionModel)
            .ToList();
        models.Add(new TestPartySizeLimitModel());
        models.Add(new TestFoodConsumptionModel());
        var gameModels = new GameModels(models);
        environment.Server.GameInstance.Game._gameModelManagers[typeof(GameModels)] = gameModels;
        Campaign.Current._gameModels = gameModels;
    }

    private sealed class TestPartySizeLimitModel : PartySizeLimitModel
    {
        public override int MinimumNumberOfVillagersAtVillagerParty => 0;
        public override ExplainedNumber GetPartyMemberSizeLimit(PartyBase party, bool isCurrentlyAtSea, bool includeDescriptions = false) => new ExplainedNumber(100f);
        public override ExplainedNumber GetPartyPrisonerSizeLimit(PartyBase party, bool includeDescriptions = false) => new ExplainedNumber(0f);
        public override ExplainedNumber CalculateGarrisonPartySizeLimit(Settlement settlement, bool includeDescriptions = false) => new ExplainedNumber(0f);
        public override int GetClanTierPartySizeEffectForHero(Hero hero) => 0;
        public override int GetNextClanTierPartySizeEffectChangeForHero(Hero hero) => 0;
        public override int GetAssumedPartySizeForLordParty(Hero leaderHero, IFaction partyMapFaction, Clan actualClan) => 0;
        public override int GetIdealVillagerPartySize(Village village) => 0;
        public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate) => null!;
        public override List<Ship> FindAppropriateInitialShipsForMobileParty(MobileParty party, PartyTemplateObject partyTemplate) => new();
    }

    private sealed class TestFoodConsumptionModel : MobilePartyFoodConsumptionModel
    {
        public override int NumberOfMenOnMapToEatOneFood => 20;
        public override ExplainedNumber CalculateDailyBaseFoodConsumptionf(MobileParty party, bool includeDescription = false) => new ExplainedNumber(0f);
        public override ExplainedNumber CalculateDailyFoodConsumptionf(MobileParty party, ExplainedNumber baseConsumption) => baseConsumption;
        public override bool DoesPartyConsumeFood(MobileParty mobileParty) => false;
    }
}
