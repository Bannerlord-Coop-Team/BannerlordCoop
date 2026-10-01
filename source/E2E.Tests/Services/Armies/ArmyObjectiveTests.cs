using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Armies.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class ArmyObjectiveTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public ArmyObjectiveTests(ITestOutputHelper output) => environment = new E2ETestEnvironment(output);

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ServerClearsObjective_RegisteredArmySurvivesOnEveryClient(bool isSettlement)
    {
        var server = environment.Server;
        string armyId = null;
        server.Call(() =>
        {
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var leader = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var army = new Army(kingdom, leader, Army.ArmyTypes.Patrolling);
            Assert.True(server.ObjectManager.TryGetId(army, out armyId));
        });
        var objectiveId = isSettlement
            ? environment.CreateRegisteredObject<Settlement>()
            : environment.CreateRegisteredObject<MobileParty>();
        var replicas = new Dictionary<EnvironmentInstance, Army>();
        foreach (var peer in environment.Clients.Append(server))
            peer.Call(() => replicas.Add(peer, peer.GetRegisteredObject<Army>(armyId)));

        server.Call(() => server.GetRegisteredObject<Army>(armyId).AiBehaviorObject =
            isSettlement ? server.GetRegisteredObject<Settlement>(objectiveId) : server.GetRegisteredObject<MobileParty>(objectiveId));
        AssertObjective(objectiveId);

        server.Call(() => server.GetRegisteredObject<Army>(armyId).AiBehaviorObject = null);
        AssertObjective(null);
        server.Call(() => server.GetRegisteredObject<Army>(armyId).AiBehaviorObject = null);
        AssertObjective(null);

        var clears = server.NetworkSentMessages.GetMessages<NetworkSetArmyAiBehaviorObject>()
            .Where(message => message.ArmyId == armyId && message.AiBehaviorObjectId == null);
        Assert.Equal(2, clears.Count());

        void AssertObjective(string expectedId)
        {
            foreach (var peer in environment.Clients.Append(server))
            {
                peer.Call(() =>
                {
                    var army = peer.GetRegisteredObject<Army>(armyId);
                    Assert.Same(replicas[peer], army);
                    Assert.False(army._armyIsDispersing);
                    if (expectedId == null)
                        Assert.Null(army.AiBehaviorObject);
                    else
                    {
                        IMapPoint expected = isSettlement
                            ? peer.GetRegisteredObject<Settlement>(expectedId)
                            : peer.GetRegisteredObject<MobileParty>(expectedId);
                        Assert.Same(expected, army.AiBehaviorObject);
                    }
                });
            }
        }
    }

    [Fact]
    public void MissingObjectiveReference_DoesNotClearExistingObjective()
    {
        var armyId = environment.CreateRegisteredObject<Army>();
        var objectiveId = environment.CreateRegisteredObject<MobileParty>();
        var client = environment.Clients.First();
        client.Call(() => client.GetRegisteredObject<Army>(armyId)._aiBehaviorObject = client.GetRegisteredObject<MobileParty>(objectiveId));

        client.SimulateMessage(environment.Server, new NetworkSetArmyAiBehaviorObject(armyId, "missing-party", false));

        client.Call(() => Assert.Same(client.GetRegisteredObject<MobileParty>(objectiveId), client.GetRegisteredObject<Army>(armyId).AiBehaviorObject));
    }
}
