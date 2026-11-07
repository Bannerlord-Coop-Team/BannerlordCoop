using E2E.Tests.Util;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

/// <summary>
/// The server handing a dead or retired player's fleet to their heir's party in place of ChangePlayerCharacterAction.
/// </summary>
public class ApplyHeirSelectionActionInterfaceTests : SyncTestBase
{
    private static readonly Vec2 OriginalAnchor = new Vec2(50f, 60f);
    private static readonly Vec2 OriginalDisembark = new Vec2(51f, 61f);

    private readonly string originalPartyId;
    private readonly string heirPartyId;
    private readonly string weakShipId;
    private readonly string strongShipId;
    private readonly string troopId;

    public ApplyHeirSelectionActionInterfaceTests(ITestOutputHelper output) : base(output)
    {
        originalPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        heirPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        weakShipId = TestEnvironment.CreateRegisteredObject<Ship>();
        strongShipId = TestEnvironment.CreateRegisteredObject<Ship>();
        troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            UseSeaMap();
            var originalParty = Server.GetRegisteredObject<MobileParty>(originalPartyId);
            GiveShip(originalParty, weakShipId, 20f);
            GiveShip(originalParty, strongShipId, 80f);
            originalParty.Anchor.SetPosition(SeaPosition(OriginalAnchor));
            originalParty.Anchor.SetLastUsedDisembarkPosition(new CampaignVec2(OriginalDisembark, true));
        });
    }

    [Fact]
    public void TransferFleetToHeir_OnLand_MovesEveryShipAndTheAnchor()
    {
        Server.Call(() =>
        {
            var heirParty = Server.GetRegisteredObject<MobileParty>(heirPartyId);
            Server.Resolve<IApplyHeirSelectionActionInterface>().TransferFleetToHeir(
                Server.GetRegisteredObject<MobileParty>(originalPartyId), heirParty);

            Assert.Equal(OriginalAnchor, heirParty.Anchor.Position.ToVec2());
            Assert.Equal(OriginalDisembark, heirParty.Anchor.GetLastUsedDisembarkPosition().ToVec2());
        });

        AssertShipOwners(weakShipOwnerId: heirPartyId, strongShipOwnerId: heirPartyId);
    }

    // A crew left at sea keeps its weakest ship so it isn't stranded without one
    [Fact]
    public void TransferFleetToHeir_AtSeaWithACrew_KeepsTheWeakestShip()
    {
        Server.Call(() =>
        {
            var originalParty = Server.GetRegisteredObject<MobileParty>(originalPartyId);
            originalParty._isCurrentlyAtSea = true;
            SetCrew(originalParty, 2);

            Server.Resolve<IApplyHeirSelectionActionInterface>().TransferFleetToHeir(
                originalParty, Server.GetRegisteredObject<MobileParty>(heirPartyId));
        });

        AssertShipOwners(weakShipOwnerId: originalPartyId, strongShipOwnerId: heirPartyId);
    }

    [Fact]
    public void TransferFleetToHeir_AtSeaWithoutACrew_MovesEveryShip()
    {
        Server.Call(() =>
        {
            var originalParty = Server.GetRegisteredObject<MobileParty>(originalPartyId);
            originalParty._isCurrentlyAtSea = true;
            SetCrew(originalParty, 1);

            Server.Resolve<IApplyHeirSelectionActionInterface>().TransferFleetToHeir(
                originalParty, Server.GetRegisteredObject<MobileParty>(heirPartyId));
        });

        AssertShipOwners(weakShipOwnerId: heirPartyId, strongShipOwnerId: heirPartyId);
    }

    // The heir's own anchor is where its fleet already is
    [Fact]
    public void TransferFleetToHeir_HeirAlreadyAnchored_KeepsTheHeirAnchor()
    {
        var heirAnchor = new Vec2(5f, 6f);

        Server.Call(() =>
        {
            var heirParty = Server.GetRegisteredObject<MobileParty>(heirPartyId);
            heirParty.Anchor.SetPosition(SeaPosition(heirAnchor));

            Server.Resolve<IApplyHeirSelectionActionInterface>().TransferFleetToHeir(
                Server.GetRegisteredObject<MobileParty>(originalPartyId), heirParty);

            Assert.Equal(heirAnchor, heirParty.Anchor.Position.ToVec2());
        });

        AssertShipOwners(weakShipOwnerId: heirPartyId, strongShipOwnerId: heirPartyId);
    }

    // A heir at sea sails with the fleet, so it isn't left anchored at the original party's coast
    [Fact]
    public void TransferFleetToHeir_HeirAtSea_LeavesTheHeirUnanchored()
    {
        Server.Call(() =>
        {
            var heirParty = Server.GetRegisteredObject<MobileParty>(heirPartyId);
            heirParty._isCurrentlyAtSea = true;

            Server.Resolve<IApplyHeirSelectionActionInterface>().TransferFleetToHeir(
                Server.GetRegisteredObject<MobileParty>(originalPartyId), heirParty);

            Assert.False(heirParty.Anchor.IsValid);
        });

        AssertShipOwners(weakShipOwnerId: heirPartyId, strongShipOwnerId: heirPartyId);
    }

    [Fact]
    public void TransferFleetToHeir_SameParty_LeavesTheFleet()
    {
        Server.Call(() =>
        {
            var originalParty = Server.GetRegisteredObject<MobileParty>(originalPartyId);
            Server.Resolve<IApplyHeirSelectionActionInterface>().TransferFleetToHeir(originalParty, originalParty);
        });

        AssertShipOwners(weakShipOwnerId: originalPartyId, strongShipOwnerId: originalPartyId);
    }

    private void GiveShip(MobileParty party, string shipId, float hitPoints)
    {
        var ship = Server.GetRegisteredObject<Ship>(shipId);
        ship.ShipHull.MaxHitPoints = 100;
        ChangeShipOwnerAction.ApplyByProduction(party.Party, ship);
        ship.HitPoints = hitPoints;
    }

    private void SetCrew(MobileParty party, int count)
    {
        party.MemberRoster.Clear();
        party.MemberRoster.AddToCounts(Server.GetRegisteredObject<CharacterObject>(troopId), count);
    }

    private void AssertShipOwners(string weakShipOwnerId, string strongShipOwnerId)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.PumpGameThread();
            Assert.Same(instance.GetRegisteredObject<MobileParty>(weakShipOwnerId).Party, instance.GetRegisteredObject<Ship>(weakShipId).Owner);
            Assert.Same(instance.GetRegisteredObject<MobileParty>(strongShipOwnerId).Party, instance.GetRegisteredObject<Ship>(strongShipId).Owner);
        }
    }

    // A position on a cached water face, which an anchor needs to be valid
    private static CampaignVec2 SeaPosition(Vec2 position) => new CampaignVec2(position, isOnLand: false)
    {
        _faceCache = new PathFaceRecord(1, 0, 0),
        _isPositionCacheValid = true,
    };

    // Call inside Server.Call. Gives the headless campaign water faces that naval navigation accepts.
    private void UseSeaMap()
    {
        var map = new Mock<IMapScene>();
        map.Setup(scene => scene.GetFaceTerrainType(It.IsAny<PathFaceRecord>())).Returns(TerrainType.Water);
        Campaign.Current._mapSceneWrapper = map.Object;
        var models = Campaign.Current.Models.GetGameModels().ToList();
        models.Add(new SeaNavigationModel());
        var gameModels = new GameModels(models);
        Server.GameInstance.Game._gameModelManagers[typeof(GameModels)] = gameModels;
        Campaign.Current._gameModels = gameModels;
    }

    private sealed class SeaNavigationModel : DefaultPartyNavigationModel
    {
        public override bool IsTerrainTypeValidForNavigationType(TerrainType terrainType, MobileParty.NavigationType navigationType) =>
            navigationType == MobileParty.NavigationType.Naval
                ? terrainType == TerrainType.Water
                : base.IsTerrainTypeValidForNavigationType(terrainType, navigationType);
    }
}
