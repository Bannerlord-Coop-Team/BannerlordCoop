using E2E.Tests.Util;
using GameInterface.Services.Villages.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class HideoutMapEventTests : MapEventTestBase
{
    public HideoutMapEventTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PlayerStartsHideoutBattle_BanditOccupantRemainsInsideAndJoinsDefenders()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("hideout-attacker");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));

            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            settlement.SetSettlementComponent(hideout);

            var banditClan = GameObjectCreator.CreateInitializedObject<Clan>();
            banditClan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            var banditParty = BanditPartyComponent.CreateBanditParty(
                "E2EHideoutDefender",
                banditClan,
                hideout,
                isBossParty: false,
                pt: null,
                new CampaignVec2(Vec2.Zero, true));

            banditParty.CurrentSettlement = settlement;
            VillageHostileFactionStanceHelper.ApplyWarStance(playerParty.MapFaction, banditParty.MapFaction);

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            mapEvent.Initialize(
                playerParty.Party,
                settlement.Party,
                new HideoutEventComponent(mapEvent, isSendTroops: false),
                MapEvent.BattleTypes.Hideout);
            mapEvent.MapEventVisual = null;

            Assert.Same(settlement, banditParty.CurrentSettlement);
            Assert.Same(mapEvent.DefenderSide, banditParty.Party.MapEventSide);
        }, MapEventDisabledMethods);
    }
}
