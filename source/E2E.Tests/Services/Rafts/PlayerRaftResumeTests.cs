using Common.Messaging;
using Common.Util;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Rafts;

/// <summary>
/// A player's raft at sea gets its move to the nearest shore again after the move was lost: on reconnect, after a
/// battle and from coop.unstuck. The server's map is a fake mesh, so the real MovePartyToTheClosestLand runs.
/// </summary>
public class PlayerRaftResumeTests : MapEventTestBase
{
    private const string ControllerId = "rafted";

    private static readonly CampaignVec2 SeaSpot = new(new Vec2(50f, 60f), false);
    private static readonly CampaignVec2 Shore = new(new Vec2(55f, 66f), true);
    private static readonly PathFaceRecord SeaFace = new(5636, 10, -1);

    private EnvironmentInstance Client => Clients.First();

    public PlayerRaftResumeTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Reconnect_ParkedRaftAtSea_MovesItToShoreEverywhere()
    {
        var (_, partyId) = CreatePlayerHeroParty(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        PutOnRaftAtSea(partyId);
        Server.Call(() => Server.GetRegisteredObject<MobileParty>(partyId).IsActive = false);
        Server.NetworkSentMessages.Clear();

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new PlayerCampaignSynchronized(Client.NetPeer)));
        Server.PumpGameThread();

        AssertMovingToShore(partyId);
        Assert.Single(ForcedUpdates());
    }

    [Fact]
    public void BattleEnd_RaftAtSea_MovesItToShoreOnceThePostBattleScreensClose()
    {
        var battle = CreateServerMapEvent();
        RegisterAsPlayerParty(ControllerId, TestEnvironment.CreateRegisteredObject<Hero>(), battle.AttackerPartyId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        var partyId = battle.AttackerPartyId;
        PutOnRaftAtSea(partyId);
        string battleStringId = null;
        Server.Call(() => battleStringId = Server.GetRegisteredObject<MapEvent>(battle.MapEventId).StringId);
        Server.NetworkSentMessages.Clear();

        DestroyServerMapEvent(battle.MapEventId);
        Server.PumpGameThread();

        Server.Call(() => Assert.Null(Server.GetRegisteredObject<MobileParty>(partyId).MapEvent));
        Assert.Empty(ForcedUpdates());

        // The server side of the client's encounter finish, which ends its post battle screens.
        Server.SimulateMessage(Client.NetPeer, new NetworkConversationEnded(finishedMapEventId: battleStringId));
        Server.PumpGameThread();

        AssertMovingToShore(partyId);
        Assert.Single(ForcedUpdates());
    }

    [Fact]
    public void ServerUnstuckRequest_OnRaftAtSea_ReportsRaftResumeStep()
    {
        var (heroId, partyId) = CreatePlayerHeroParty(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        PutOnRaftAtSea(partyId);
        Server.NetworkSentMessages.Clear();

        Server.SimulateMessage(Client.NetPeer, new NetworkRequestPlayerUnstuck(partyId, heroId));
        Server.PumpGameThread();

        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
        Assert.Contains("Sent the raft toward the nearest shore at 55,66.", result.Actions);
        AssertMovingToShore(partyId);
        Assert.Single(ForcedUpdates());
    }

    // Leaving the battle resumes the raft through PartyOccupancyChanged, so the unstuck step must not send it again.
    [Fact]
    public void ServerUnstuckRequest_OnRaftInABattle_MovesItToShoreOnce()
    {
        var battle = CreateServerMapEvent();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty(ControllerId, heroId, battle.AttackerPartyId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        var partyId = battle.AttackerPartyId;
        PutOnRaftAtSea(partyId);
        Server.NetworkSentMessages.Clear();

        Server.SimulateMessage(Client.NetPeer, new NetworkRequestPlayerUnstuck(partyId, heroId));
        Server.PumpGameThread();

        AssertMovingToShore(partyId);
        Assert.Single(ForcedUpdates());
    }

    // A reload sets the raft state straight into its field, so only the switch to the player reopens the raft menu.
    [Fact]
    public void SwitchToPlayer_PartyOnRaft_OpensTheRaftMenu()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        using var menuRecorder = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }));

        Client.Call(() =>
        {
            var party = Client.GetRegisteredObject<MobileParty>(partyId);
            var hero = party.LeaderHero;
            Assert.True(Client.ObjectManager.TryGetId(hero, out var heroId));
            Assert.True(Client.ObjectManager.TryGetId(hero.Clan, out var clanId));
            Assert.True(Client.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
            party._isInRaftState = true;

            Client.Resolve<IHeroInterface>().SwitchToPlayer(new Player(ControllerId, heroId, partyId, clanId, characterId));
        }, new[]
        {
            AccessTools.Method(typeof(InteractionsInitializationHandler), "Handle", new[] { typeof(MessagePayload<PlayerHeroChanged>) }),
        });
        Client.PumpGameThread();

        Assert.Equal(new[] { "player_raft_state" }, menuRecorder.MenusFor(Client));
    }

    // Sets the party on a raft at sea on the server, over a fake mesh whose closest reachable land is the shore.
    private void PutOnRaftAtSea(string partyId) => Server.Call(() =>
    {
        var mapScene = new Mock<IMapScene>();
        mapScene.Setup(scene => scene.GetFaceIndex(in It.Ref<CampaignVec2>.IsAny)).Returns(SeaFace);
        mapScene.Setup(scene => scene.GetFaceTerrainType(It.IsAny<PathFaceRecord>())).Returns(TerrainType.Water);
        mapScene.Setup(scene => scene.GetNearestFaceCenterForPositionWithPath(It.IsAny<PathFaceRecord>(), true, It.IsAny<float>(), It.IsAny<int[]>()))
            .Returns(Shore);
        Campaign.Current._mapSceneWrapper = mapScene.Object;

        // MovePartyToTheClosestLand reads Campaign.Current.Models.PartyNavigationModel, which the harness campaign doesnt have
        var models = Campaign.Current.Models.GetGameModels().ToList();
        models.Add(new DefaultPartyNavigationModel());
        var gameModels = new GameModels(models);
        Server.GameInstance.Game._gameModelManagers[typeof(GameModels)] = gameModels;
        Campaign.Current._gameModels = gameModels;

        var party = Server.GetRegisteredObject<MobileParty>(partyId);
        using (new AllowedThread())
            party.Position = SeaSpot;
        party._isCurrentlyAtSea = true;
        party.IsInRaftState = true;
    });

    private void AssertMovingToShore(string partyId)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.Equal(MoveModeType.Point, party.PartyMoveMode);
                Assert.Equal(Shore.ToVec2(), party.MoveTargetPoint.ToVec2());
            });
        }
    }

    private IEnumerable<NetworkUpdatePartyBehavior> ForcedUpdates() =>
        Server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>()
            .Where(update => update.BehaviorUpdateData.ForcePosition);
}
