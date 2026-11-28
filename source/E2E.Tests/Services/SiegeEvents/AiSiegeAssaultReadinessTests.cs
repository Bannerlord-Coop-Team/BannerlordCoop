using Common.Util;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>Exercises the server's patched assault roll with modeled equipment progress.</summary>
public class AiSiegeAssaultReadinessTests : MapEventTestBase
{
    public AiSiegeAssaultReadinessTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PreparedAiSiege_WithEquipmentPenaltyOverOne_IsReadyToBesiege()
    {
        Server.Call(() =>
        {
            Settlement settlement;
            MobileParty attacker;
            MobileParty defender;
            BesiegerCamp camp;
            using (new AllowedThread())
            {
                settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
                settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Town>());
                attacker = GameObjectCreator.CreateInitializedObject<MobileParty>();
                defender = GameObjectCreator.CreateInitializedObject<MobileParty>();
                defender._currentSettlement = settlement;
                defender.Aggressiveness = 1f;
                CreateReplicableSiegeEvent(settlement, attacker);
                camp = settlement.SiegeEvent.BesiegerCamp;
                var preparations = new SiegeEvent.SiegeEngineConstructionProgress(new SiegeEngineType(), 1f, 100f);
                camp.SiegeEngines = new SiegeEvent.SiegeEnginesContainer(BattleSideEnum.Attacker, preparations);
            }

            var encounter = new Mock<DefaultEncounterModel>();
            encounter.Setup(model => model.GetDefenderPartiesOfSettlement(settlement, MapEvent.BattleTypes.Siege))
                .Returns(new[] { defender.Party });

            // The 200:1 strength ratio puts the capped assault chance above 1, so the random roll always passes.
            var power = new Mock<DefaultMilitaryPowerModel>();
            power.Setup(model => model.GetPowerOfParty(attacker.Party, It.IsAny<BattleSideEnum>(), It.IsAny<MapEvent.PowerCalculationContext>()))
                .Returns(2000f);
            power.Setup(model => model.GetPowerOfParty(defender.Party, It.IsAny<BattleSideEnum>(), It.IsAny<MapEvent.PowerCalculationContext>()))
                .Returns(10f);

            var combat = new Mock<DefaultCombatSimulationModel>();
            combat.Setup(model => model.GetSettlementAdvantage(settlement)).Returns(1f);
            // Two built engines and one half built give 0.5 + 0.25 * 3 = 1.25 before the cap.
            combat.Setup(model => model.GetNumberOfEquipmentsBuilt(settlement)).Returns(2);
            combat.Setup(model => model.GetMaximumSiegeEquipmentProgress(settlement)).Returns(0.5f);
            var models = Campaign.Current.Models.GetGameModels().ToList();
            models.AddRange(new GameModel[] { encounter.Object, power.Object, combat.Object });
            Campaign.Current._gameModels = new GameModels(models);

            Assert.True(camp.IsPreparationComplete);
            Assert.True(camp.IsReadyToBesiege);
        }, SiegeCreationDisabledMethods);
    }
}
