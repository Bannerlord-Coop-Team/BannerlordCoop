using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyBases;
public class SyncPartyBaseTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    EnvironmentInstance Server => TestEnvironment.Server;

    IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    private readonly string PartyBaseId;
    private readonly string ItemRosterId;
    private readonly string MapEventSideId;
    private readonly string TroopRosterId;
    private readonly string MobilePartyId;
    private readonly string SettlementId;

    public SyncPartyBaseTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Server_PartyBase()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        const float newFloat = 551;
        const int newInt = 42;

        string? PartyBaseId = null;
        string? ItemRosterId = null;
        string? MapEventSideId = null;
        string? TroopRosterId = null;
        string? MobilePartyId = null;
        string? SettlementId = null;

    server.Call(() =>
        {
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var partyBase = new PartyBase(mobileParty);
            //var itemRoster = new ItemRoster();
            var mapEventSide = GameObjectCreator.CreateInitializedObject<MapEventSide>();
            var troopRoster = new TroopRoster();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();

            // Create objcets on the server
            Assert.True(Server.ObjectManager.AddNewObject(partyBase, out PartyBaseId));
            //Assert.True(Server.ObjectManager.AddNewObject(itemRoster, out ItemRosterId));
            Assert.True(Server.ObjectManager.AddNewObject(mapEventSide, out MapEventSideId));
            Assert.True(Server.ObjectManager.AddNewObject(troopRoster, out TroopRosterId));
            Assert.True(Server.ObjectManager.AddNewObject(mobileParty, out MobilePartyId));
            Assert.True(Server.ObjectManager.AddNewObject(settlement, out SettlementId));

            Assert.True(server.ObjectManager.TryGetObject<PartyBase>(PartyBaseId, out var serverPartyBase));
            //Assert.True(server.ObjectManager.TryGetObject<ItemRoster>(ItemRosterId, out var serverItemRoster));
            Assert.True(server.ObjectManager.TryGetObject<MapEventSide>(MapEventSideId, out var serverMapEventSide));
            Assert.True(server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var serverTroopRoster));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var serverMobileParty));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(SettlementId, out var serverSettlement));


            serverPartyBase.AverageBearingRotation = newFloat;
            serverPartyBase.IsVisualDirty = true;
            //serverPartyBase.ItemRoster = serverItemRoster;
            serverPartyBase.LevelMaskIsDirty = true;
            serverPartyBase.MapEventSide = serverMapEventSide;
            serverPartyBase.MemberRoster = serverTroopRoster;
            serverPartyBase.MobileParty = serverMobileParty;
            serverPartyBase.PrisonRoster = serverTroopRoster;
            serverPartyBase.RandomValue = newInt;
            serverPartyBase.RemainingFoodPercentage = newInt;
            serverPartyBase.Settlement = serverSettlement;

            Assert.Equal(newFloat, serverPartyBase.AverageBearingRotation);
            Assert.True(serverPartyBase.IsVisualDirty);
            //Assert.Equal(serverItemRoster, serverPartyBase.ItemRoster);
            Assert.True(serverPartyBase.LevelMaskIsDirty);
            Assert.Equal(serverMapEventSide, serverPartyBase.MapEventSide);
            Assert.Equal(serverTroopRoster, serverPartyBase.MemberRoster);
            Assert.Equal(serverMobileParty, serverPartyBase.MobileParty);
            Assert.Equal(serverTroopRoster, serverPartyBase.PrisonRoster);
            Assert.Equal(newInt, serverPartyBase.RandomValue);
            Assert.Equal(newInt, serverPartyBase.RemainingFoodPercentage);
            Assert.Equal(serverSettlement, serverPartyBase.Settlement);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(PartyBaseId, out var clientPartyBase));
            //Assert.True(server.ObjectManager.TryGetObject<ItemRoster>(ItemRosterId, out var clientItemRoster));
            Assert.True(server.ObjectManager.TryGetObject<MapEventSide>(MapEventSideId, out var clientMapEventSide));
            Assert.True(server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var clientTroopRoster));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(SettlementId, out var clientSettlement));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(MobilePartyId, out var clientMobileParty));

            Assert.Equal(newFloat, clientPartyBase.AverageBearingRotation);
            Assert.True(clientPartyBase.IsVisualDirty);
            //Assert.Equal(clientItemRoster, clientPartyBase.ItemRoster);
            Assert.True(clientPartyBase.LevelMaskIsDirty);
            Assert.Equal(clientMapEventSide, clientPartyBase.MapEventSide);
            Assert.Equal(clientTroopRoster, clientPartyBase.MemberRoster);
            Assert.Equal(clientMobileParty, clientPartyBase.MobileParty);
            Assert.Equal(clientTroopRoster, clientPartyBase.PrisonRoster);
            Assert.Equal(newInt, clientPartyBase.RandomValue);
            Assert.Equal(newInt, clientPartyBase.RemainingFoodPercentage);
            Assert.Equal(clientSettlement, clientPartyBase.Settlement);
        }
    }
}
