using Common.Messaging;
using Common.Util;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace E2E.Tests.Services.MobileParties;

/// <summary>
/// Verifies the repair of a player party whose server copy stands on a map spot its navigation
/// cannot use (ticket 4182: the water beside a town after a lost siege). Native movement never
/// leaves such a spot, so the server copy froze while the client kept walking.
/// </summary>
public class UnstuckMapPositionTests : MapEventTestBase
{
    private const string ControllerId = "unstuck-position";

    private delegate PathFaceRecord FaceIndexLookup(in CampaignVec2 position);
    private delegate CampaignVec2 NearestFaceCenterLookup(in CampaignVec2 position, int[] excludedFaceIds);

    // The save's frozen spot sits on a water face outside every navigation island; the closest
    // usable face center is about a unit away.
    private static readonly CampaignVec2 StuckSpot = Position(405.195f, 292.296f);
    private static readonly CampaignVec2 ClosestUsableSpot = Position(406.047f, 292.602f);
    private static readonly CampaignVec2 VillageGate = Position(416.539f, 309.282f);
    // A scrap of plain land on its own navigation island, with no settlement gate on it.
    private static readonly CampaignVec2 CutOffSpot = Position(400f, 290f);

    private static readonly PathFaceRecord WaterFace = new(5636, 10, -1);
    private static readonly PathFaceRecord CutOffLandFace = new(7001, 1, 4);
    private static readonly PathFaceRecord MainLandFace = new(12990, 1, 9);

    private EnvironmentInstance Client => Clients.First();

    public UnstuckMapPositionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PlayerMoveOrder_FromUnusableSpot_FreesTheServerCopyOnEveryInstance()
    {
        var partyId = PrepareConnectedPlayer(StuckSpot);
        Server.Call(() => UseNavigationMesh(closestFaceCenter: ClosestUsableSpot));

        // The same publish the server's behavior request packet handler makes for a client order.
        Server.Call(() =>
        {
            var order = new PartyBehaviorUpdateData(
                Compact(partyId, typeof(MobileParty)),
                AiBehavior.GoToPoint,
                null,
                VillageGate,
                StuckSpot,
                AiBehavior.GoToPoint,
                VillageGate,
                MobileParty.NavigationType.Default)
            {
                OriginControllerId = ControllerId,
                PartyMoveMode = MoveModeType.Point,
                MoveTargetPoint = VillageGate,
            };
            Server.Resolve<IMessageBroker>().Publish(this, new UpdatePartyBehavior(ref order, alreadyOnGameThread: true));
        });

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.Equal(ClosestUsableSpot, party.Position);
            Assert.Equal(MoveModeType.Point, party.PartyMoveMode);
            Assert.Equal(VillageGate, party.MoveTargetPoint);
            Assert.Equal(MobileParty.NavigationType.None, party._lastComputedPathNavigationType);
        });
        Client.Call(() => Assert.Equal(ClosestUsableSpot, Client.GetRegisteredObject<MobileParty>(partyId).Position));
        AssertForcedPositionSent();
    }

    [Fact]
    public void Unstuck_OnUnusableSpot_MovesTheServerCopyToTheClosestUsableSpot()
    {
        var partyId = PrepareConnectedPlayer(StuckSpot);
        Server.Call(() => UseNavigationMesh(closestFaceCenter: ClosestUsableSpot));

        Server.SimulateMessage(Client.NetPeer, new NetworkRequestPlayerUnstuck(partyId, null));

        foreach (var instance in Clients.Append(Server))
            instance.Call(() => Assert.Equal(ClosestUsableSpot, instance.GetRegisteredObject<MobileParty>(partyId).Position));
        AssertForcedPositionSent();
        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
        Assert.Contains("Moved the party from 405.2,292.3, a map spot it could not move from, to 406.05,292.6.", result.Actions);
    }

    [Fact]
    public void Unstuck_WhenTheClosestFaceIsUnusableToo_MovesToTheNearestUsableGate()
    {
        var partyId = PrepareConnectedPlayer(StuckSpot);
        Server.Call(() =>
        {
            // The closest face center lies in the same water.
            UseNavigationMesh(closestFaceCenter: Position(405.5f, 292.3f));
            CreateTown(Position(450f, 350f));
        });

        Server.SimulateMessage(Client.NetPeer, new NetworkRequestPlayerUnstuck(partyId, null));

        foreach (var instance in Clients.Append(Server))
            instance.Call(() => Assert.Equal(VillageGate, instance.GetRegisteredObject<MobileParty>(partyId).Position));
        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
        Assert.Contains(result.Actions, action => action.EndsWith("to 416.54,309.28."));
    }

    [Fact]
    public void Unstuck_OnUsableSpot_LeavesTheMapPositionAlone()
    {
        var partyId = PrepareConnectedPlayer(ClosestUsableSpot);
        Server.Call(() => UseNavigationMesh(closestFaceCenter: ClosestUsableSpot));

        Server.SimulateMessage(Client.NetPeer, new NetworkRequestPlayerUnstuck(partyId, null));

        Server.Call(() => Assert.Equal(ClosestUsableSpot, Server.GetRegisteredObject<MobileParty>(partyId).Position));
        Assert.DoesNotContain(
            Server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>(),
            update => update.BehaviorUpdateData.ForcePosition);
        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
        Assert.Contains("No server-side stuck state found (captivity, map event, army, siege camp, settlement, map position).", result.Actions);
    }

    [Fact]
    public void Unstuck_OnValidLandCutOffFromEveryGate_MovesToTheClosestReachableSpot()
    {
        var partyId = PrepareConnectedPlayer(StuckSpot);
        // Plain terrain passes the native terrain check, but no gate shares its navigation island.
        Server.Call(() => UseNavigationMesh(closestFaceCenter: ClosestUsableSpot, stuckFace: CutOffLandFace));

        Server.SimulateMessage(Client.NetPeer, new NetworkRequestPlayerUnstuck(partyId, null));

        foreach (var instance in Clients.Append(Server))
            instance.Call(() => Assert.Equal(ClosestUsableSpot, instance.GetRegisteredObject<MobileParty>(partyId).Position));
        AssertForcedPositionSent();
    }

    [Fact]
    public void Unstuck_WhenTheClosestFaceIsValidLandCutOffFromEveryGate_MovesToTheNearestUsableGate()
    {
        var partyId = PrepareConnectedPlayer(StuckSpot);
        Server.Call(() => UseNavigationMesh(closestFaceCenter: CutOffSpot));

        Server.SimulateMessage(Client.NetPeer, new NetworkRequestPlayerUnstuck(partyId, null));

        foreach (var instance in Clients.Append(Server))
            instance.Call(() => Assert.Equal(VillageGate, instance.GetRegisteredObject<MobileParty>(partyId).Position));
    }

    [Fact]
    public void Unstuck_ArmyLeaderOnUnusableSpot_MovesTheAttachedPartiesWithIt()
    {
        var partyId = PrepareConnectedPlayer(StuckSpot);
        var memberId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                using (new AllowedThread())
                    instance.GetRegisteredObject<MobileParty>(memberId).Position = StuckSpot;
            });
        Server.Call(() =>
        {
            UseNavigationMesh(closestFaceCenter: ClosestUsableSpot);
            var leader = Server.GetRegisteredObject<MobileParty>(partyId);
            var member = Server.GetRegisteredObject<MobileParty>(memberId);
            var army = new Army(GameObjectCreator.CreateInitializedObject<Kingdom>(), leader, Army.ArmyTypes.Patrolling);
            member.Army = army;
            member.AttachedTo = leader;
            member.IsActive = true;
            Assert.Contains(member, leader.AttachedParties);
        });

        Server.SimulateMessage(Client.NetPeer, new NetworkRequestPlayerUnstuck(partyId, null));

        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                Assert.Equal(ClosestUsableSpot, instance.GetRegisteredObject<MobileParty>(partyId).Position);
                Assert.Equal(ClosestUsableSpot, instance.GetRegisteredObject<MobileParty>(memberId).Position);
            });
        // One forced update for the leader and a separate one for the attached party.
        var forcedPartyIds = Server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>()
            .Where(update => update.BehaviorUpdateData.ForcePosition)
            .Select(update => update.BehaviorUpdateData.MobilePartyId)
            .Distinct();
        Assert.Equal(2, forcedPartyIds.Count());
    }

    /// <summary>Registers a connected player whose party stands at <paramref name="position"/> on every instance.</summary>
    private string PrepareConnectedPlayer(CampaignVec2 position)
    {
        var (heroId, partyId) = CreatePlayerHeroParty(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Server.GetRegisteredObject<Hero>(heroId).PartyBelongedTo = party;
            party.IsActive = true;
        });
        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                using (new AllowedThread())
                    instance.GetRegisteredObject<MobileParty>(partyId).Position = position;
            });
        return partyId;
    }

    /// <summary>
    /// Gives the server a navigation mesh where <see cref="StuckSpot"/> lies on
    /// <paramref name="stuckFace"/> (water outside every island unless given), the land around
    /// <see cref="CutOffSpot"/> is its own island and the rest is the main land holding the town gate
    /// at <see cref="VillageGate"/>, with <paramref name="closestFaceCenter"/> as the closest face
    /// center the native lookup returns.
    /// </summary>
    private static void UseNavigationMesh(CampaignVec2 closestFaceCenter, PathFaceRecord? stuckFace = null)
    {
        var faceAtStuckSpot = stuckFace ?? WaterFace;
        var mapScene = new Mock<IMapScene>();
        mapScene.Setup(scene => scene.GetFaceIndex(in It.Ref<CampaignVec2>.IsAny))
            .Returns(new FaceIndexLookup((in CampaignVec2 position) =>
                position.ToVec2().Distance(StuckSpot.ToVec2()) < 0.5f ? faceAtStuckSpot :
                position.ToVec2().Distance(CutOffSpot.ToVec2()) < 0.5f ? CutOffLandFace :
                MainLandFace));
        mapScene.Setup(scene => scene.GetFaceTerrainType(It.IsAny<PathFaceRecord>()))
            .Returns<PathFaceRecord>(face => face.FaceIndex == WaterFace.FaceIndex ? TerrainType.Water : TerrainType.Plain);
        mapScene.Setup(scene => scene.GetNearestFaceCenterForPosition(in It.Ref<CampaignVec2>.IsAny, It.IsAny<int[]>()))
            .Returns(new NearestFaceCenterLookup((in CampaignVec2 position, int[] excludedFaceIds) => closestFaceCenter));
        Campaign.Current._mapSceneWrapper = mapScene.Object;
        CreateTown(VillageGate);
    }

    private static void CreateTown(CampaignVec2 gate)
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Town>());
        settlement._position = gate;
        settlement.GatePosition = gate;
        Campaign.Current.CampaignObjectManager.Settlements =
            new MBList<Settlement>(Campaign.Current.CampaignObjectManager.Settlements) { settlement };
    }

    private void AssertForcedPositionSent()
    {
        Assert.Contains(
            Server.NetworkSentMessages.GetMessages<NetworkUpdatePartyBehavior>(),
            update => update.BehaviorUpdateData.ForcePosition);
    }

    private static CampaignVec2 Position(float x, float y) => new(new Vec2(x, y), true);
}
