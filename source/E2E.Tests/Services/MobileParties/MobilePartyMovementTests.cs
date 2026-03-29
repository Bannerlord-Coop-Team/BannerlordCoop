using Autofac;
using Common.Util;
using E2E.Tests.Util;
using GameInterface.DynamicSync;
using GameInterface.Services.Entity;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

public class MobilePartyMovementTests : SyncTestBase
{
    private readonly string MobilePartyId = "TestParty";
    private readonly string TargetPartyId = "TargetParty";
    private readonly string TargetSettlementId = "TargetSettlement";

    private readonly MobileParty ServerParty;
    private readonly List<MobileParty> ClientParties = new();

    public MobilePartyMovementTests(ITestOutputHelper output) : base(output)
    {
        ServerParty = CreateParty(MobilePartyId);
        TestEnvironment.Server.ObjectManager.AddExisting(MobilePartyId, ServerParty);
        TestEnvironment.Server.Container.Resolve<IControllerIdProvider>().SetControllerId($"TestServer");

        TargetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TargetSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();


        var clientNum = 1;

        foreach (var client in TestEnvironment.Clients)
        {
            var clientParty = CreateParty(MobilePartyId);

            client.ObjectManager.AddExisting(MobilePartyId, clientParty);

            client.Container.Resolve<IControllerIdProvider>().SetControllerId($"TestClient{clientNum++}");

            ClientParties.Add(clientParty);
        }
    }

    private MobileParty CreateParty(string stringId)
    {
        using (new AllowedThread())
        {
            var party = new MobileParty();

            party.StringId = stringId;

            party.Aggressiveness = 1f;
            party.Ai = new MobilePartyAi(party);
            party._formationPosition.x = 10000f;
            party._formationPosition.y = 10000f;
            while (party._formationPosition.LengthSquared > 0.36f || party._formationPosition.LengthSquared < 0.22f)
            {
                party._formationPosition = new Vec2(MBRandom.RandomFloat * 1.2f - 0.6f, MBRandom.RandomFloat * 1.2f - 0.6f);
            }

            return party;
        }
    }

    [Fact]
    public void Party_SetMoveHold_Sync()
    {
        // Arrange

        // Act
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        server.Call(() =>
        {
            ServerParty.SetMoveModeHold();
            ServerParty.Ai.Tick(dt);
        });

        foreach (var (client, clientParty) in TestEnvironment.Clients.Zip(ClientParties))
        {
            client.Call(() =>
            {
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var (client, clientParty) in TestEnvironment.Clients.Zip(ClientParties))
        {
            Assert.Equal(ServerParty.DefaultBehavior, clientParty.DefaultBehavior);
            Assert.Equal(ServerParty.ShortTermBehavior, clientParty.ShortTermBehavior);
            Assert.Equal(ServerParty.TargetPosition, clientParty.TargetPosition);
            Assert.Equal(ServerParty.MoveTargetPoint, clientParty.MoveTargetPoint);
            Assert.Equal(ServerParty.DesiredAiNavigationType, clientParty.DesiredAiNavigationType);

            Assert.True(client.ObjectManager.TryGetId(clientParty.TargetParty, out var targetPartyId));
            Assert.True(client.ObjectManager.TryGetId(clientParty.TargetSettlement, out var targetSettlementId));
            Assert.Equal(TargetSettlementId, targetSettlementId);
            Assert.Equal(TargetPartyId, targetPartyId);
        }
    }

    [Fact]
    public void Party_SetMoveEngageParty_Sync()
    {
        // Arrange

        // Act
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        server.Call(() =>
        {
            server.ObjectManager.TryGetObject<MobileParty>(TargetPartyId, out var targetParty);
            ServerParty.SetMoveEngageParty(targetParty, MobileParty.NavigationType.Default);
            ServerParty.Ai.Tick(dt);
        });

        foreach (var (client, clientParty) in TestEnvironment.Clients.Zip(ClientParties))
        {
            client.Call(() =>
            {
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var (client, clientParty) in TestEnvironment.Clients.Zip(ClientParties))
        {
            Assert.Equal(ServerParty.DefaultBehavior, clientParty.DefaultBehavior);
            Assert.Equal(ServerParty.ShortTermBehavior, clientParty.ShortTermBehavior);
            Assert.Equal(ServerParty.TargetPosition, clientParty.TargetPosition);
            Assert.Equal(ServerParty.MoveTargetPoint, clientParty.MoveTargetPoint);
            Assert.Equal(ServerParty.DesiredAiNavigationType, clientParty.DesiredAiNavigationType);

            Assert.True(client.ObjectManager.TryGetId(clientParty.TargetParty, out var targetPartyId));
            Assert.True(client.ObjectManager.TryGetId(clientParty.TargetSettlement, out var targetSettlementId));
            Assert.Equal(TargetSettlementId, targetSettlementId);
            Assert.Equal(TargetPartyId, targetPartyId);
        }
    }

    [Fact]
    public void PartyAi_SetMoveGoToPoint_Sync()
    {
        // Arrange

        // Act
        var dt = 0.1f;
        var point = new Vec2(0.1f, 0.2f);
        var campaignPoint = new CampaignVec2(point, true);

        var server = TestEnvironment.Server;

        server.Call(() =>
        {
            ServerParty.SetMoveGoToPoint(campaignPoint, MobileParty.NavigationType.Default);
            ServerParty.Ai.Tick(dt);
        });

        foreach (var (client, clientParty) in TestEnvironment.Clients.Zip(ClientParties))
        {
            client.Call(() =>
            {
                clientParty.Ai.Tick(dt);
            });
        }

        // Assert
        foreach (var clientParty in ClientParties)
        {
            Assert.Equal(ServerParty.TargetPosition, clientParty.TargetPosition);
        }
    }
}
