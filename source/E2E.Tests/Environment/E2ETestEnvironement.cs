using Autofac;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Environment;

/// <summary>
/// Testing environment for End to End testing
/// </summary>
internal class E2ETestEnvironement
{
    public TestEnvironment IntegrationEnvironment { get; }

    public ITestOutputHelper Output { get; }

    public IEnumerable<EnvironmentInstance> Clients => IntegrationEnvironment.Clients;
    public EnvironmentInstance Server => IntegrationEnvironment.Server;
    public E2ETestEnvironement(ITestOutputHelper output, int numClients = 2)
    {
        GameBootStrap.Initialize();
        IntegrationEnvironment = new TestEnvironment(numClients);

        var gameInterface = Server.Container.Resolve<IGameInterface>();

        gameInterface.PatchAll();

        foreach(var settlement in Campaign.Current.CampaignObjectManager.Settlements)
        {
            Server.ObjectManager.AddExisting(settlement.StringId, settlement);
        }

        Output = output;
    }
}
