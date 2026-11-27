using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// A clan changing kingdom runs vanilla's siege continuity check on the server. A player it pulls out of its own
/// siege gets the "no longer enemies" menu, which the client's own run never shows because the leave arrives first.
/// </summary>
public class SiegeKingdomChangeTests : SiegeTestBase
{
    private const string PlayerControllerId = "SiegeKingdomJoiner";
    private const string PeaceMenuId = "hostile_action_end_by_peace";

    public SiegeKingdomChangeTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void CampedPlayerJoinsKingdomAtPeaceWithDefenders_LeavesTheSiegeWithTheNoLongerEnemiesMenu()
    {
        var client = Clients.First();
        var scenario = SetupCampedPlayerScenario(client);

        using var activated = new MethodCallRecorder(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu)));
        using var switched = new MethodCallRecorder(
            AccessTools.Method(typeof(MenuContext), nameof(MenuContext.SwitchToMenu), new[] { typeof(string) }));

        Server.Call(() =>
        {
            var clan = Server.GetRegisteredObject<Clan>(scenario.PlayerClanId);
            var kingdom = Server.GetRegisteredObject<Kingdom>(scenario.JoinedKingdomId);
            ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom);
        });
        client.PumpGameThread();

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var siegeEvent = instance.GetRegisteredObject<SiegeEvent>(scenario.Siege.SiegeEventId);
                var leader = instance.GetRegisteredObject<MobileParty>(scenario.LeaderPartyId);
                var player = instance.GetRegisteredObject<MobileParty>(scenario.PlayerPartyId);

                Assert.Null(player.BesiegerCamp);
                Assert.Same(siegeEvent.BesiegerCamp, leader.BesiegerCamp);
                Assert.Equal(new[] { leader }, siegeEvent.BesiegerCamp._besiegerParties);
                Assert.Same(siegeEvent, siegeEvent.BesiegedSettlement.SiegeEvent);
            });
        }

        var menus = activated.MenusFor(client).Concat(switched.MenusFor(client));
        Assert.Equal(new[] { PeaceMenuId }, menus);
        Assert.Empty(activated.MenusFor(Clients.Last()).Concat(switched.MenusFor(Clients.Last())));
        client.Call(() =>
        {
            var behavior = Campaign.Current.GetCampaignBehavior<PartyDiplomaticHandlerCampaignBehavior>();
            var defenders = client.GetRegisteredObject<Kingdom>(scenario.DefenderKingdomId);
            Assert.Same(defenders, behavior._lastFactionMadePeaceWithCausedPlayerToLeaveEvent);
        });
    }

    private CampedPlayerScenario SetupCampedPlayerScenario(EnvironmentInstance client)
    {
        var besiegerKingdomId = CreateKingdomEverywhere();
        var defenderKingdomId = CreateKingdomEverywhere();
        var joinedKingdomId = CreateKingdomEverywhere();
        var besiegerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var ownerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var leaderPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var (playerHeroId, playerPartyId) = CreatePlayerHeroParty(PlayerControllerId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var besiegers = instance.GetRegisteredObject<Kingdom>(besiegerKingdomId);
                var defenders = instance.GetRegisteredObject<Kingdom>(defenderKingdomId);
                var besiegerClan = instance.GetRegisteredObject<Clan>(besiegerClanId);
                var ownerClan = instance.GetRegisteredObject<Clan>(ownerClanId);
                var playerClan = instance.GetRegisteredObject<Clan>(playerClanId);
                var playerHero = instance.GetRegisteredObject<Hero>(playerHeroId);

                using (new AllowedThread())
                {
                    JoinKingdom(besiegerClan, besiegers);
                    JoinKingdom(ownerClan, defenders);
                    JoinKingdom(playerClan, besiegers);
                    playerClan.Name = new TextObject("joiner clan");
                    playerHero.Clan = playerClan;
                    playerClan.SetLeader(playerHero);
                    playerHero.PartyBelongedTo = instance.GetRegisteredObject<MobileParty>(playerPartyId);
                    instance.GetRegisteredObject<MobileParty>(playerPartyId).ActualClan = playerClan;
                    instance.GetRegisteredObject<MobileParty>(leaderPartyId).ActualClan = besiegerClan;
                    VillageHostileFactionStanceHelper.ApplyWarStance(besiegers, defenders);
                }
            });
        }

        var siege = SetupSiege(leaderPartyId, playerPartyId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var siegeEvent = instance.GetRegisteredObject<SiegeEvent>(siege.SiegeEventId);
                var settlement = siegeEvent.BesiegedSettlement;
                var town = instance.GetRegisteredObject<Town>(townId);

                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(town);
                    town._ownerClan = instance.GetRegisteredObject<Clan>(ownerClanId);
                    settlement.SiegeEvent = siegeEvent;
                    // The live server starts sieges through SiegeEventManager.StartSiegeEvent, which lists them.
                    if (!Campaign.Current.SiegeEventManager._siegeEvents.Contains(siegeEvent))
                        Campaign.Current.SiegeEventManager._siegeEvents.Add(siegeEvent);
                }

                var diplomaticHandler = new PartyDiplomaticHandlerCampaignBehavior();
                Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                    new CampaignBehaviorBase[] { diplomaticHandler }));
                diplomaticHandler.RegisterEvents();

                Assert.True(settlement.IsFortification);
                Assert.Same(instance.GetRegisteredObject<Kingdom>(defenderKingdomId), settlement.MapFaction);
                Assert.True(siegeEvent.CanPartyJoinSide(
                    instance.GetRegisteredObject<MobileParty>(playerPartyId).Party, BattleSideEnum.Attacker));
            });
        }

        ConfigureCampedClient(client, playerPartyId);

        return new CampedPlayerScenario(
            siege, defenderKingdomId, joinedKingdomId, playerClanId, playerPartyId, leaderPartyId);
    }

    private string CreateKingdomEverywhere()
    {
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var kingdom = instance.GetRegisteredObject<Kingdom>(kingdomId);
                using (new AllowedThread())
                {
                    kingdom._activePolicies ??= new MBList<PolicyObject>();
                    kingdom._armies ??= new MBList<Army>();
                    kingdom._clans ??= new MBList<Clan>();
                    kingdom._unresolvedDecisions ??= new MBList<TaleWorlds.CampaignSystem.Election.KingdomDecision>();
                    kingdom._factionsAtWarWith ??= new MBList<IFaction>();
                    kingdom._alliedKingdoms ??= new MBList<Kingdom>();

                    if (!Campaign.Current.CampaignObjectManager.Kingdoms.Contains(kingdom))
                        Campaign.Current.CampaignObjectManager.AddKingdom(kingdom);
                }
            });
        }

        return kingdomId;
    }

    private static void JoinKingdom(Clan clan, Kingdom kingdom)
    {
        clan._kingdom = kingdom;
        kingdom._rulingClan ??= clan;
        if (!kingdom._clans.Contains(clan))
            kingdom._clans.Add(clan);
    }

    private static void ConfigureCampedClient(EnvironmentInstance client, string mainPartyId)
    {
        client.Call(() =>
        {
            var mainParty = client.GetRegisteredObject<MobileParty>(mainPartyId);
            Assert.NotNull(mainParty.BesiegerCamp);
            Campaign.Current.MainParty = mainParty;
            Campaign.Current.PlayerEncounter = null;

            var gameStateManager = Game.Current.GameStateManager;
            var mapState = gameStateManager.CreateState<MapState>();
            var menuContext = ObjectHelper.SkipConstructor<MenuContext>();
            menuContext.GameMenu = new GameMenu("menu_siege_strategies");
            mapState._menuContext = menuContext;
            gameStateManager._gameStates.Add(mapState);
        });
    }

    private readonly record struct CampedPlayerScenario(
        SiegeContext Siege,
        string DefenderKingdomId,
        string JoinedKingdomId,
        string PlayerClanId,
        string PlayerPartyId,
        string LeaderPartyId);
}
