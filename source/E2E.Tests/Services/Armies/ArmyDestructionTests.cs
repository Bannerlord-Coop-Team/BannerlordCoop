using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Armies;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class ArmyDestructionTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public ArmyDestructionTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerDestroyArmy_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string? armyId = null;
        server.Call(() =>
        {

            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();


            var army = new Army(kingdom, mobileParty, Army.ArmyTypes.Patrolling);

            Assert.True(server.ObjectManager.TryGetId(army, out armyId));
        });

        Assert.NotNull(armyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var _));
        }

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Army>(armyId, out var army));

            DisbandArmyAction.ApplyByObjectiveFinished(army);
        }, new[] { AccessTools.Method(typeof(PartyBase), nameof(PartyBase.UpdateVisibilityAndInspected)) });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Army>(armyId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Army>(armyId, out var _));
        }
    }

    [Fact]
    public void ClientDestroyArmy_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        string? armyId = null;
        server.Call(() =>
        {

            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

            var army = new Army(kingdom, mobileParty, Army.ArmyTypes.Patrolling);

            Assert.True(server.ObjectManager.TryGetId(army, out armyId));
        });

        Assert.NotNull(armyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var _));
        }

        // Act
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Army>(armyId, out var army));

            DisbandArmyAction.ApplyByObjectiveFinished(army);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Army>(armyId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var _));
        }
    }

    [Fact]
    public void ServerDisbandArmyWithoutMainParty_PreservesLocalCameraTargets()
    {
        var server = TestEnvironment.Server;
        string? armyId = null;
        string? leaderPartyId = null;
        string? observerPartyId = null;

        server.Call(() =>
        {
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var leaderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var army = new Army(kingdom, leaderParty, Army.ArmyTypes.Patrolling);

            Assert.True(server.ObjectManager.TryGetId(army, out armyId));
            Assert.True(server.ObjectManager.TryGetId(leaderParty, out leaderPartyId));
            var observerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(server.ObjectManager.TryGetId(observerParty, out observerPartyId));
            Campaign.Current.MainParty = null;
            Assert.Null(MobileParty.MainParty);
        });

        var armyOwner = TestEnvironment.Clients.First();
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                var localPartyId = client == armyOwner ? leaderPartyId : observerPartyId;
                Campaign.Current.MainParty = client.GetRegisteredObject<MobileParty>(localPartyId);
                Assert.False(Hero.MainHero.IsPrisoner);
                Campaign.Current.CameraFollowParty = MobileParty.MainParty.Party;
            });
        }

        server.NetworkSentMessages.Clear();
        server.Call(() =>
        {
            var army = server.GetRegisteredObject<Army>(armyId);
            var leaderParty = server.GetRegisteredObject<MobileParty>(leaderPartyId);
            server.Resolve<IArmyDisbander>().Disband(army, Army.ArmyDispersionReason.Unknown);

            Assert.Null(leaderParty.Army);
            Assert.False(server.ObjectManager.TryGetObject<Army>(armyId, out _));
        });

        var removal = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>());
        Assert.Equal(leaderPartyId, removal.MobilePartyId);
        Assert.Empty(removal.ClientMobilePartyId);
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.False(client.ObjectManager.TryGetObject<Army>(armyId, out _));
                var leaderParty = client.GetRegisteredObject<MobileParty>(leaderPartyId);
                Assert.Null(leaderParty.Army);
                var localPartyId = client == armyOwner ? leaderPartyId : observerPartyId;
                var localParty = client.GetRegisteredObject<MobileParty>(localPartyId);
                Assert.Same(localParty.Party, Campaign.Current.CameraFollowParty);
            });
        }
    }

    // The kingdom screen disbands an army the sender's party is not in, paid from the sender's own clan.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Disband_IsAppliedOnlyAsTheSendersOwnParty(bool ownParty)
    {
        var server = TestEnvironment.Server;
        var (armyId, memberPartyId) = CreateArmyWithMember();
        var senderPartyId = RegisterDisbandingPlayer();

        Send(new NetworkDisbandArmyApplyInternal(armyId, ownParty ? senderPartyId : memberPartyId));

        server.Call(() => Assert.Equal(ownParty, server.GetRegisteredObject<MobileParty>(memberPartyId).Army == null));
    }

    [Fact]
    public void DisbandFromTheKingdomScreenOfAnArmyInABattle_IsIgnored()
    {
        var server = TestEnvironment.Server;
        var (armyId, memberPartyId) = CreateArmyWithMember();
        var senderPartyId = RegisterDisbandingPlayer();
        server.Call(() =>
        {
            // A stale kingdom screen can still send the disband after the army's leader joined a battle.
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            mapEvent.Initialize(
                GameObjectCreator.CreateInitializedObject<MobileParty>().Party,
                GameObjectCreator.CreateInitializedObject<MobileParty>().Party,
                new FieldBattleEventComponent(mapEvent),
                MapEvent.BattleTypes.FieldBattle);
            mapEvent.MapEventVisual = null;
            server.GetRegisteredObject<Army>(armyId).LeaderParty.Party.MapEventSide = mapEvent.AttackerSide;
        }, new[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });

        Send(new NetworkDisbandArmyApplyInternal(armyId, senderPartyId));

        server.Call(() =>
        {
            var army = server.GetRegisteredObject<Army>(armyId);
            Assert.Same(army, server.GetRegisteredObject<MobileParty>(memberPartyId).Army);
            Assert.Equal(1000f, server.GetRegisteredObject<MobileParty>(senderPartyId).LeaderHero.Clan.Influence, precision: 2);
        });
    }

    // Only the army's leader boosts its cohesion.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CohesionBoost_IsAppliedOnlyFromTheArmyLeader(bool fromLeader)
    {
        var server = TestEnvironment.Server;
        var (armyId, _) = CreateArmyWithMember();
        string? leaderPartyId = null;
        server.Call(() =>
        {
            var army = server.GetRegisteredObject<Army>(armyId);
            var leader = army.LeaderParty.LeaderHero;
            leader.Clan ??= GameObjectCreator.CreateInitializedObject<Clan>();
            leader.Clan.Influence = 1000f;
            army.Cohesion = 20f;
            Assert.True(server.ObjectManager.TryGetId(army.LeaderParty, out leaderPartyId));
        });
        RegisterPlayer(fromLeader ? leaderPartyId! : TestEnvironment.CreateRegisteredObject<MobileParty>());

        Send(new NetworkPlayerBoostedArmyCohesion(leaderPartyId, 10f, 0));

        server.Call(() => Assert.Equal(fromLeader ? 30f : 20f, server.GetRegisteredObject<Army>(armyId).Cohesion));
    }

    private (string ArmyId, string MemberPartyId) CreateArmyWithMember()
    {
        var server = TestEnvironment.Server;
        var armyId = TestEnvironment.CreateRegisteredObject<Army>();
        var memberPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        server.Call(() =>
        {
            var army = server.GetRegisteredObject<Army>(armyId);
            var member = server.GetRegisteredObject<MobileParty>(memberPartyId);
            member.Army = army;
            member.AttachedTo = army.LeaderParty;
        });
        return (armyId, memberPartyId);
    }

    // A player with its own clan and influence, outside the army.
    private string RegisterDisbandingPlayer()
    {
        var server = TestEnvironment.Server;
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        server.Call(() =>
        {
            var clan = server.GetRegisteredObject<MobileParty>(partyId).LeaderHero.Clan ??= GameObjectCreator.CreateInitializedObject<Clan>();
            clan.Influence = 1000f;
            // Vanilla's disband cost reads Clan.PlayerClan, which the test campaign leaves unset.
            Campaign.Current.PlayerDefaultFaction = clan;
        });
        RegisterPlayer(partyId);
        return partyId;
    }

    private void RegisterPlayer(string partyId)
    {
        var server = TestEnvironment.Server;
        server.Call(() =>
        {
            var party = server.GetRegisteredObject<MobileParty>(partyId);
            Assert.True(server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
            var playerManager = server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("Sender", heroId, partyId, null, null)));
            playerManager.SetPeer("Sender", TestEnvironment.Clients.First().NetPeer);
        });
    }

    private void Send<T>(T message) where T : Common.Messaging.IMessage
    {
        TestEnvironment.Server.SimulateMessage(TestEnvironment.Clients.First().NetPeer, message);
        TestEnvironment.Server.PumpGameThread();
    }
}
