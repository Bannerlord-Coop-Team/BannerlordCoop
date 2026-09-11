#if DEBUG
using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Missions.Battles;
using Missions.Messages;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// The synthetic lab over the existing campaign and mission routers, with only native scene/ship calls
/// replaced. No campaign battle is created. Readiness callbacks are driven explicitly by each test.
/// </summary>
public class NavalMissionTestEnvironment : MissionTestEnvironment, IDisposable
{
    private readonly MissionEngineFixture fixture = new();
    private readonly string? previousCapability = ModInformation.NavalLabCapability;
    private bool disposed;
    protected EnvironmentInstance First => Clients.First();
    protected EnvironmentInstance Second => Clients.Last();
    protected TestNetworkRouter CampaignRouter => Server.Resolve<TestNetworkRouter>();
    protected NavalLabManifest Manifest => Server.Resolve<INavalLabSessionStore>().Current;

    public NavalMissionTestEnvironment(ITestOutputHelper output, int numClients = 2)
        : base(output, numClients, configureDependencies: builder =>
        {
            builder.RegisterType<NavalTestAdapterLoader>().AsSelf()
                .As<INavalMissionAdapterLoader>().InstancePerLifetimeScope();
        })
    {
        ModInformation.ConfigureNavalLab("new-campaign:42d6555a-345a-475b-ab23-5cfe26c9127f", "naval-e2e", true);
        int index = 0;
        foreach (var client in Clients)
        {
            string id = index++ == 0 ? "naval-A" : "naval-B";
            SetControllerId(client, id);
            client.Call(() => Adapter(client).Bind(fixture.CreateMission(client)));
            Server.Call(() =>
            {
                // Only authenticated connection identity is needed by the isolated lab, not a campaign party.
                var players = Server.Resolve<IPlayerManager>();
                players.AddPlayer(new Player(id, "", "", "", ""));
                players.SetPeer(id, client.NetPeer);
            });
        }
        CampaignRouter.ReceiveContext = TestNetworkReceiveContext.PollerThread;
        First.Resolve<MeshNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
    }

    protected NavalTestAdapter Adapter(EnvironmentInstance client) =>
        client.Resolve<NavalTestAdapterLoader>().Adapter;

    protected Guid CreateLab(NavalLabMode mode = NavalLabMode.Activation)
    {
        var operation = Guid.NewGuid();
        Server.Call(() => Server.Resolve<INavalLabCoordinator>().Create(operation, "naval-A", "naval-B", mode));
        PumpAll();
        Assert.Equal(0, Server.Resolve<NavalTestAdapterLoader>().LoadCount);
        Assert.NotSame(Adapter(First), Adapter(Second));
        return operation;
    }

    protected void Ready(EnvironmentInstance client)
    {
        client.Call(() => Adapter(client).Controller!.AfterStart());
        PumpAll();
    }

    protected void StartReleased(NavalLabMode mode = NavalLabMode.Activation)
    {
        CreateLab(mode);
        Ready(First);
        Ready(Second);
        Assert.Single(Actions(First), action => action.Kind == "release");
        Assert.Single(Actions(Second), action => action.Kind == "release");
    }

    protected Guid Execute(string kind, int ship = 0, float value = 0, bool row = false, Guid? operation = null)
    {
        Guid id = operation ?? Guid.NewGuid();
        Server.Call(() => Server.Resolve<INavalLabCoordinator>().Execute(id, kind, ship, value, row));
        PumpAll();
        return id;
    }

    protected void Tick(EnvironmentInstance client, float dt = 0.05f)
    {
        client.Call(() => Adapter(client).Controller!.OnMissionTick(dt));
        PumpAll();
    }

    protected void SendAction(EnvironmentInstance recipient, NetworkNavalLabAction action)
    {
        Server.Call(() => Server.Resolve<INetwork>().Send(recipient.NetPeer, action));
        PumpAll();
    }

    protected void SendFrames(EnvironmentInstance sender, NetworkNavalLabFrames frames)
    {
        sender.Call(() => sender.Resolve<MockBattleNetwork>().SendAll(frames));
        PumpAll();
    }

    protected static NetworkNavalLabAction[] Actions(EnvironmentInstance client) =>
        client.InternalMessages.GetMessages<NetworkNavalLabAction>().ToArray();

    protected static string Receipt(EnvironmentInstance client, Guid operation) =>
        Assert.Single(client.NetworkSentMessages.GetMessages<NetworkNavalLabReceipt>(),
            receipt => receipt.OperationId == operation).Status;

    protected JObject Samples(EnvironmentInstance client) =>
        JObject.FromObject(client.Resolve<INavalLabCoordinator>().Samples(0));

    protected void PumpAll()
    {
        for (int pass = 0; pass < 20; pass++)
        {
            CampaignRouter.DrainReady();
            Server.PumpGameThread();
            foreach (var client in Clients) client.PumpGameThread();
            if (Server.PendingGameThreadActionCount == 0
                && Clients.All(client => client.PendingGameThreadActionCount == 0)) return;
        }
        Assert.Fail("Naval lab traffic did not settle within 20 game-thread pump rounds.");
    }

    public new void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            foreach (var client in Clients)
                client.Call(() => Adapter(client).Controller?.AbortStart());
            PumpAll();
        }
        finally
        {
            try { base.Dispose(); }
            finally
            {
                fixture.Dispose();
                // Common is not publicized; restore the process opt-in just like the naval unit fixtures.
                typeof(ModInformation).GetProperty(nameof(ModInformation.NavalLabCapability))!
                    .SetValue(null, previousCapability);
            }
        }
    }
}
#endif
