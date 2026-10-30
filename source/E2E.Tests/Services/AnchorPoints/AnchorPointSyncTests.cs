using E2E.Tests.Util;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.AnchorPoints;

/// <summary>
/// A party's anchored fleet: the server owns where it is, and a client embarking from it asks the server to set sail there.
/// </summary>
public class AnchorPointSyncTests : SyncTestBase
{
    private static readonly Vec2 AnchorPosition = new Vec2(20f, 30f);

    private readonly string partyId;

    public AnchorPointSyncTests(ITestOutputHelper output) : base(output)
    {
        partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
    }

    [Fact]
    public void ClientEmbarkAtAnchor_ServerSetsSailThereForEveryone()
    {
        AnchorPartyOnLand();
        var client = Clients.First();

        client.Call(() =>
        {
            var party = client.GetRegisteredObject<MobileParty>(partyId);
            party.Anchor.OnPartyInteraction(party);

            // The client only asks, the server moves the party
            Assert.False(party.IsCurrentlyAtSea);
            Assert.Null(party.Ai.AiBehaviorInteractable);
        });
        Server.PumpGameThread();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.True(party.IsCurrentlyAtSea);
            Assert.Equal(AnchorPosition, party.Position.ToVec2());
            Assert.False(party.Anchor.IsValid);
        });
        foreach (var instance in Clients)
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.True(party.IsCurrentlyAtSea);
                Assert.Equal(AnchorPosition, party.Position.ToVec2());
            });
        }
    }

    // A second request that arrives after the party already set sail does nothing
    [Fact]
    public void ClientEmbarkAtAnchor_PartyAlreadyAtSea_IsIgnored()
    {
        AnchorPartyOnLand();
        Server.Call(() => Server.GetRegisteredObject<MobileParty>(partyId)._isCurrentlyAtSea = true);
        var client = Clients.First();

        client.Call(() =>
        {
            var party = client.GetRegisteredObject<MobileParty>(partyId);
            party.Anchor.OnPartyInteraction(party);
        });
        Server.PumpGameThread();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.Equal(AnchorPosition, party.Anchor.Position.ToVec2());
            Assert.NotEqual(AnchorPosition, party.Position.ToVec2());
        });
    }

    [Fact]
    public void ServerSetSettlement_SyncsTheAnchorAndDisembarkPositions()
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var port = new Vec2(70f, 80f);
        var gate = new Vec2(71f, 81f);

        Server.Call(() =>
        {
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            settlement.PortPosition = new CampaignVec2(port, false);
            settlement.GatePosition = new CampaignVec2(gate, true);
            Server.GetRegisteredObject<MobileParty>(partyId).Anchor.SetSettlement(settlement);
        });

        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() =>
            {
                var anchor = client.GetRegisteredObject<MobileParty>(partyId).Anchor;
                Assert.Equal(port, anchor.Position.ToVec2());
                Assert.Equal(gate, anchor.GetLastUsedDisembarkPosition().ToVec2());
            });
        }
    }

    // A client's own writes would desync its copy of the fleet, so only the server moves an anchor
    [Fact]
    public void ClientAnchorWrites_DoNothing()
    {
        var client = Clients.First();

        client.Call(() =>
        {
            var anchor = client.GetRegisteredObject<MobileParty>(partyId).Anchor;
            anchor.SetPosition(new CampaignVec2(AnchorPosition, false));
            anchor.SetLastUsedDisembarkPosition(new CampaignVec2(AnchorPosition, true));

            Assert.NotEqual(AnchorPosition, anchor.Position.ToVec2());
            Assert.NotEqual(AnchorPosition, anchor.GetLastUsedDisembarkPosition().ToVec2());
        });
    }

    [Fact]
    public void ServerAnchorIsDisabled_SyncsToClients()
    {
        Server.Call(() => Server.GetRegisteredObject<MobileParty>(partyId).Anchor.IsDisabled = true);

        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() => Assert.True(client.GetRegisteredObject<MobileParty>(partyId).Anchor.IsDisabled));
        }
    }

    // The party waits on land with its fleet anchored on a valid water face offshore
    private void AnchorPartyOnLand()
    {
        Server.Call(() =>
        {
            UseSeaMap();
            Server.GetRegisteredObject<MobileParty>(partyId).Anchor.SetPosition(new CampaignVec2(AnchorPosition, isOnLand: false)
            {
                _faceCache = new PathFaceRecord(1, 0, 0),
                _isPositionCacheValid = true,
            });
        });

        foreach (var client in Clients)
        {
            client.PumpGameThread();
        }
    }

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
