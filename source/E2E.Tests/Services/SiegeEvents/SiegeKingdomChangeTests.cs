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
        var bystander = Clients.Last();
        var scenario = SetupSiegeScenario(playerInCamp: true);
        ConfigureSiegeMenuClient(client, scenario.PlayerPartyId);
        // Its own main party takes the bystander's prompt handler past the null check to the party filter.
        ConfigureSiegeMenuClient(bystander, scenario.LeaderPartyId);

        using var menus = new MenuRecorder();
        JoinKingdomOnServer(scenario.PlayerClanId, scenario.NeutralKingdomId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var siegeEvent = instance.GetRegisteredObject<SiegeEvent>(scenario.SiegeEventId);
                var leader = instance.GetRegisteredObject<MobileParty>(scenario.LeaderPartyId);

                Assert.Null(instance.GetRegisteredObject<MobileParty>(scenario.PlayerPartyId).BesiegerCamp);
                Assert.Equal(new[] { leader }, siegeEvent.BesiegerCamp._besiegerParties);
                Assert.Same(siegeEvent, siegeEvent.BesiegedSettlement.SiegeEvent);
            });
        }

        Assert.Equal(new[] { PeaceMenuId }, menus.MenusFor(client));
        Assert.Empty(menus.MenusFor(bystander));
        AssertPeaceMenuFaction(client, scenario.DefenderKingdomId);
    }

    [Fact]
    public void DefendingPlayerJoinsTheBesiegers_LeavesTheTownWithTheNoLongerEnemiesMenu()
    {
        var client = Clients.First();
        var scenario = SetupSiegeScenario(playerInCamp: false);
        ConfigureSiegeMenuClient(client, scenario.PlayerPartyId);

        using var menus = new MenuRecorder();
        JoinKingdomOnServer(scenario.PlayerClanId, scenario.BesiegerKingdomId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var siegeEvent = instance.GetRegisteredObject<SiegeEvent>(scenario.SiegeEventId);
                var player = instance.GetRegisteredObject<MobileParty>(scenario.PlayerPartyId);

                Assert.Null(player.CurrentSettlement);
                Assert.DoesNotContain(player, siegeEvent.BesiegedSettlement.Parties);
                Assert.Same(siegeEvent, siegeEvent.BesiegedSettlement.SiegeEvent);
            });
        }

        Assert.Equal(new[] { PeaceMenuId }, menus.MenusFor(client));
        AssertPeaceMenuFaction(client, scenario.BesiegerKingdomId);
    }

    private void JoinKingdomOnServer(string clanId, string kingdomId)
    {
        Server.Call(() => ChangeKingdomAction.ApplyByJoinToKingdom(
            Server.GetRegisteredObject<Clan>(clanId),
            Server.GetRegisteredObject<Kingdom>(kingdomId)));
    }

    private static void AssertPeaceMenuFaction(EnvironmentInstance client, string kingdomId)
    {
        client.Call(() => Assert.Same(
            client.GetRegisteredObject<Kingdom>(kingdomId),
            Campaign.Current.GetCampaignBehavior<PartyDiplomaticHandlerCampaignBehavior>()
                ._lastFactionMadePeaceWithCausedPlayerToLeaveEvent));
    }

    /// <summary>
    /// The besieger kingdom's lord party besieges a town of the defender kingdom, at war, with the player either
    /// camped alongside it as a besieger vassal or inside the town as a defender vassal.
    /// </summary>
    private SiegeScenario SetupSiegeScenario(bool playerInCamp)
    {
        var besiegerKingdomId = CreateKingdomEverywhere();
        var defenderKingdomId = CreateKingdomEverywhere();
        var neutralKingdomId = CreateKingdomEverywhere();
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
                var playerClan = instance.GetRegisteredObject<Clan>(playerClanId);
                var playerHero = instance.GetRegisteredObject<Hero>(playerHeroId);
                var playerParty = instance.GetRegisteredObject<MobileParty>(playerPartyId);

                using (new AllowedThread())
                {
                    JoinKingdom(instance.GetRegisteredObject<Clan>(besiegerClanId), besiegers);
                    JoinKingdom(instance.GetRegisteredObject<Clan>(ownerClanId), defenders);
                    JoinKingdom(playerClan, playerInCamp ? besiegers : defenders);
                    playerClan.Name = new TextObject("joiner clan");
                    playerHero.Clan = playerClan;
                    playerClan.SetLeader(playerHero);
                    playerHero.PartyBelongedTo = playerParty;
                    playerParty.ActualClan = playerClan;
                    instance.GetRegisteredObject<MobileParty>(leaderPartyId).ActualClan =
                        instance.GetRegisteredObject<Clan>(besiegerClanId);
                    VillageHostileFactionStanceHelper.ApplyWarStance(besiegers, defenders);
                }
            });
        }

        var siege = playerInCamp ? SetupSiege(leaderPartyId, playerPartyId) : SetupSiege(leaderPartyId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var siegeEvent = instance.GetRegisteredObject<SiegeEvent>(siege.SiegeEventId);
                var settlement = siegeEvent.BesiegedSettlement;
                var town = instance.GetRegisteredObject<Town>(townId);
                var playerParty = instance.GetRegisteredObject<MobileParty>(playerPartyId);

                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(town);
                    town._ownerClan = instance.GetRegisteredObject<Clan>(ownerClanId);
                    settlement.SiegeEvent = siegeEvent;
                    // The live server starts sieges through SiegeEventManager.StartSiegeEvent, which lists them.
                    if (!Campaign.Current.SiegeEventManager._siegeEvents.Contains(siegeEvent))
                        Campaign.Current.SiegeEventManager._siegeEvents.Add(siegeEvent);

                    if (!playerInCamp)
                    {
                        playerParty._currentSettlement = settlement;
                        settlement._partiesCache.Add(playerParty);
                    }
                }

                var diplomaticHandler = new PartyDiplomaticHandlerCampaignBehavior();
                Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                    new CampaignBehaviorBase[] { diplomaticHandler }));
                diplomaticHandler.RegisterEvents();

                Assert.True(settlement.IsFortification);
                Assert.Same(instance.GetRegisteredObject<Kingdom>(defenderKingdomId), settlement.MapFaction);
                Assert.True(siegeEvent.CanPartyJoinSide(
                    playerParty.Party, playerInCamp ? BattleSideEnum.Attacker : BattleSideEnum.Defender));
            });
        }

        return new SiegeScenario(
            siege.SiegeEventId,
            besiegerKingdomId,
            defenderKingdomId,
            neutralKingdomId,
            playerClanId,
            playerPartyId,
            leaderPartyId);
    }

    private string CreateKingdomEverywhere()
    {
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        foreach (var instance in Clients.Append(Server))
        {
            KingdomTestSetup.EnsureKingdomRegistered(instance, kingdomId);
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

    /// <summary>Records both ways a menu opens, without running them.</summary>
    private sealed class MenuRecorder : IDisposable
    {
        private readonly MethodCallRecorder activated =
            new(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu)));
        private readonly MethodCallRecorder switched =
            new(AccessTools.Method(typeof(MenuContext), nameof(MenuContext.SwitchToMenu), new[] { typeof(string) }));

        public string[] MenusFor(EnvironmentInstance instance) =>
            activated.MenusFor(instance).Concat(switched.MenusFor(instance)).ToArray();

        public void Dispose()
        {
            activated.Dispose();
            switched.Dispose();
        }
    }

    private readonly record struct SiegeScenario(
        string SiegeEventId,
        string BesiegerKingdomId,
        string DefenderKingdomId,
        string NeutralKingdomId,
        string PlayerClanId,
        string PlayerPartyId,
        string LeaderPartyId);
}
