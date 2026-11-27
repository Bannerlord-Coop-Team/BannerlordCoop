using Common.Network.Messages;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Util;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements;

/// <summary>
/// A war that turns the town a player is in hostile, which vanilla handles for the main party with the slink away menu.
/// </summary>
public class WarDeclaredInTownTests : SyncTestBase
{
    public WarDeclaredInTownTests(ITestOutputHelper output) : base(output)
    {
    }

    // #4857: the server ejected the party like an AI party, which finished the encounter under a running tournament.
    [Fact]
    public void WarOnPlayersTown_KeepsPartyInsideUntilSlinkAwayContinue()
    {
        var playerClient = Clients.First();
        var player = CreatePlayer(playerClient, "player");
        var other = CreatePlayer(Clients.Last(), "other");
        var (settlementId, townClanId) = CreateTownWith(player, other);

        PlayerEncounter townEncounter = null;
        playerClient.Call(() =>
        {
            Campaign.Current.MapStateData ??= new MapStateData();
            townEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            townEncounter.EncounterSettlementAux = playerClient.GetRegisteredObject<Settlement>(settlementId);
            Campaign.Current.PlayerEncounter = townEncounter;
        });

        using var menus = new MethodCallRecorder(Priority.First, AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)));
        DeclareWar(townClanId, player);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>());
        AssertInTown(player.MobilePartyId, settlementId, inTown: true);
        AssertInTown(other.MobilePartyId, settlementId, inTown: true);
        playerClient.Call(() =>
        {
            Assert.True(FactionManager.IsAtWarAgainstFaction(playerClient.GetRegisteredObject<Clan>(townClanId), Hero.MainHero.MapFaction));
            Assert.Same(townEncounter, PlayerEncounter.Current);
        });
        Assert.Equal(new[] { "fortification_crime_rating" }, menus.MenusFor(playerClient));

        // The slink away menu's continue leaves through the server
        playerClient.Call(() => ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>()
            .game_menu_fortification_high_crime_rating_continue_on_consequence(new MenuCallbackArgs((MenuContext)null, null)));
        Assert.Single(playerClient.NetworkSentMessages.GetMessages<NetworkRequestEndSettlementEncounter>());
        Flush();

        AssertInTown(player.MobilePartyId, settlementId, inTown: false);
        AssertInTown(other.MobilePartyId, settlementId, inTown: true);
        playerClient.Call(() => Assert.Null(PlayerEncounter.Current));
    }

    // An offline player has no client to open the slink away menu, whether it was offline at the war or drops before continuing
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OfflinePlayerInHostileTown_ServerEjectsParty(bool dropsAfterWar)
    {
        var playerClient = Clients.First();
        var player = CreatePlayer(dropsAfterWar ? playerClient : null, "player");
        var (settlementId, townClanId) = CreateTownWith(player);

        using var menus = new MethodCallRecorder(Priority.First, AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)));
        DeclareWar(townClanId, player);
        if (dropsAfterWar)
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>());
            Server.SimulateMessage(this, new PlayerDisconnected(playerClient.NetPeer, default));
            Flush();
        }

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>());
        AssertInTown(player.MobilePartyId, settlementId, inTown: false);
    }

    private (string SettlementId, string TownClanId) CreateTownWith(params Player[] players)
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var townClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                using var scope = new AllowedThread();
                var settlement = instance.GetRegisteredObject<Settlement>(settlementId);
                var town = instance.GetRegisteredObject<Town>(townId);
                var townClan = instance.GetRegisteredObject<Clan>(townClanId);
                // Stance links are keyed by faction id, which the harness leaves unset
                townClan.Id = new MBGUID(4857);
                instance.GetRegisteredObject<Clan>(players[0].ClanId).Id = new MBGUID(4858);
                settlement.SetSettlementComponent(town);
                town._ownerClan = townClan;
                townClan._settlementsCache.Add(settlement);
                foreach (var player in players)
                    instance.GetRegisteredObject<MobileParty>(player.MobilePartyId).CurrentSettlement = settlement;

                var diplomaticHandler = new PartyDiplomaticHandlerCampaignBehavior();
                diplomaticHandler.RegisterEvents();
                if (instance == Server)
                    Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { diplomaticHandler }));
            });
        }
        return (settlementId, townClanId);
    }

    private void DeclareWar(string townClanId, Player player)
    {
        Server.Call(() => DeclareWarAction.ApplyInternal(
            Server.GetRegisteredObject<Clan>(townClanId),
            Server.GetRegisteredObject<Clan>(player.ClanId),
            DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision));
        Flush();
    }

    private void AssertInTown(string partyId, string settlementId, bool inTown)
    {
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() => Assert.Same(
                inTown ? instance.GetRegisteredObject<Settlement>(settlementId) : null,
                instance.GetRegisteredObject<MobileParty>(partyId).CurrentSettlement));
        }
    }

    // Two passes, so a reply the server sends from a game-thread action reaches the clients too
    private void Flush()
    {
        for (int i = 0; i < 2; i++)
        {
            Server.PumpGameThread();
            TestEnvironment.FlushCoalescer();
            foreach (var client in Clients) client.PumpGameThread();
        }
    }
}
