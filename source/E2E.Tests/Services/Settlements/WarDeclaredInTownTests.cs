using Common.Network.Messages;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
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
        // Gives the second client its own main party, which every real client has, waiting in the town like B in the manual test
        var other = CreatePlayer(Clients.Last(), "other");
        var (settlementId, townClanId) = HostileTownSetup.CreateTown(TestEnvironment, player.ClanId, player.MobilePartyId, other.MobilePartyId);

        // Puts the client's main party in a town encounter the way entering the town would. PlayerEncounter.Finish writes
        // MapStateData.GameMenuId when no menu is open, which the harness campaign doesn't create
        PlayerEncounter townEncounter = null;
        playerClient.Call(() =>
        {
            Campaign.Current.MapStateData ??= new MapStateData();
            townEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            townEncounter.EncounterSettlementAux = playerClient.GetRegisteredObject<Settlement>(settlementId);
            Campaign.Current.PlayerEncounter = townEncounter;
        });

        using var menus = new MethodCallRecorder(Priority.First, AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)));
        HostileTownSetup.DeclareWar(TestEnvironment, townClanId, player.ClanId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>());
        HostileTownSetup.AssertInTown(TestEnvironment, player.MobilePartyId, settlementId, inTown: true);
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
        TestEnvironment.PumpAndFlush();

        HostileTownSetup.AssertInTown(TestEnvironment, player.MobilePartyId, settlementId, inTown: false);
        playerClient.Call(() => Assert.Null(PlayerEncounter.Current));
    }

    // An offline player has no client to open the slink away menu, so the stance change ejects its party
    [Fact]
    public void OfflineAtWar_ServerEjectsParty()
    {
        var player = CreatePlayer(null, "player");
        var (settlementId, townClanId) = HostileTownSetup.CreateTown(TestEnvironment, player.ClanId, player.MobilePartyId);

        HostileTownSetup.DeclareWar(TestEnvironment, townClanId, player.ClanId);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>());
        HostileTownSetup.AssertInTown(TestEnvironment, player.MobilePartyId, settlementId, inTown: false);
    }

    // A player who drops before pressing continue leaves no client to open the menu, so parking its party ejects it
    [Fact]
    public void DropsBeforeSlinkAwayContinue_ServerEjectsPartyOnPark()
    {
        var playerClient = Clients.First();
        var player = CreatePlayer(playerClient, "player");
        var (settlementId, townClanId) = HostileTownSetup.CreateTown(TestEnvironment, player.ClanId, player.MobilePartyId);

        HostileTownSetup.DeclareWar(TestEnvironment, townClanId, player.ClanId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>());
        Server.SimulateMessage(this, new PlayerDisconnected(playerClient.NetPeer, default));
        TestEnvironment.PumpAndFlush();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>());
        HostileTownSetup.AssertInTown(TestEnvironment, player.MobilePartyId, settlementId, inTown: false);
    }
}
