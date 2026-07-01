using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using SandBox.GauntletUI.Map;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// E2E test environment for the battle host/mission stack. Extends <see cref="E2ETestEnvironment"/> with
/// helpers to stand up a coop battle (a <see cref="MapEvent"/> whose sides hold one player party per
/// controller id) and to drive + inspect server-authoritative host election.
/// <para>
/// Scope: host election and (future) migration travel the campaign <c>INetwork</c>, which the E2E mock
/// router replicates, so they are testable here. Troop spawning and control transfer travel the P2P mesh
/// (<c>IBattleNetwork</c>) and also need a live <see cref="TaleWorlds.MountAndBlade.Mission"/> — neither
/// exists in this headless harness — so those mechanisms are exercised by targeted unit tests instead.
/// </para>
/// </summary>
public class MissionTestEnvironment : E2ETestEnvironment
{
    /// <summary>Methods to suppress when constructing a <see cref="MapEvent"/> headlessly.</summary>
    protected IReadOnlyList<MethodBase> MapEventDisabledMethods { get; }

    public MissionTestEnvironment(ITestOutputHelper output, int numClients = 2) : base(output, numClients)
    {
        MapEventDisabledMethods = new List<MethodBase>
        {
            // The real map-event visual needs a live render context; the mocked visual NREs in Initialize.
            AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)),
        };
    }

    /// <summary>Sets a client's network identity — the value host election ranks by.</summary>
    protected void SetControllerId(EnvironmentInstance instance, string controllerId)
    {
        instance.Call(() => instance.Resolve<IControllerIdProvider>().SetControllerId(controllerId));
    }

    /// <summary>
    /// Stands up a coop field battle: one player <see cref="MobileParty"/> per supplied controller id, all in
    /// a single <see cref="MapEvent"/> and registered as players on every instance, with client <c>i</c> given
    /// <c>controllerIds[i]</c>. Returns the map event id and the per-controller party ids.
    /// </summary>
    protected (string mapEventId, string[] partyIds) SetupCoopBattle(params string[] controllerIds)
    {
        Assert.True(controllerIds.Length >= 2, "Need at least two players for a battle");

        var clients = Clients.ToArray();
        for (int i = 0; i < controllerIds.Length && i < clients.Length; i++)
            SetControllerId(clients[i], controllerIds[i]);

        string? mapEventId = null;
        var partyIds = new string[controllerIds.Length];

        Server.Call(() =>
        {
            var parties = new MobileParty[controllerIds.Length];
            for (int i = 0; i < parties.Length; i++)
                parties[i] = GameObjectCreator.CreateInitializedObject<MobileParty>();

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            mapEvent.Initialize(parties[0].Party, parties[1].Party);

            // Any additional players reinforce the attacker side (coop allies).
            for (int i = 2; i < parties.Length; i++)
                parties[i].Party.MapEventSide = mapEvent.AttackerSide;

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            for (int i = 0; i < parties.Length; i++)
                Assert.True(Server.ObjectManager.TryGetId(parties[i], out partyIds[i]));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);

        for (int i = 0; i < controllerIds.Length; i++)
        {
            var heroId = CreateRegisteredObject<Hero>();
            RegisterAsPlayerParty(controllerIds[i], heroId, partyIds[i]);
        }

        return (mapEventId!, partyIds);
    }

    /// <summary>Registers a hero/party pair as a player on every instance (controller id → party).</summary>
    protected void RegisterAsPlayerParty(string controllerId, string heroId, string partyId)
    {
        void Register(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                var registry = instance.Resolve<IPlayerManager>();
                registry.AddPlayer(new Player(controllerId, heroId, partyId, "MyClanId", "MyCharacterObjectId"));
                Assert.True(registry.TryGetPlayer(controllerId, out _));
            });
        }

        Register(Server);
        foreach (var client in Clients)
            Register(client);
    }

    /// <summary>
    /// Simulates <paramref name="client"/> opening the battle mission: publishes <see cref="PlayerEnteredBattle"/>,
    /// which makes its <c>BattleHostHandler</c> request election from the server. The whole round-trip runs
    /// synchronously through the mock network (GameThread.Run is inline on the test's game thread).
    /// </summary>
    protected void EnterBattle(EnvironmentInstance client, string mapEventId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            client.Resolve<IMessageBroker>().Publish(this, new PlayerEnteredBattle(mapEvent));
        });
    }

    /// <summary>
    /// Simulates the server observing <paramref name="controllerId"/> leave/drop from the battle instance:
    /// publishes <see cref="MissionMemberDeparted"/> on the server, which drives host migration (promote the
    /// next successor) or successor-line cleanup. The promotion broadcast travels the mock campaign network.
    /// </summary>
    protected void DepartBattle(string controllerId, string mapEventId, bool wasRetreat = false)
    {
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(this, new MissionMemberDeparted(controllerId, mapEventId, wasRetreat));
        });
    }

    /// <summary>Asserts the elected host and successor order recorded on <paramref name="instance"/>.</summary>
    protected void AssertHost(EnvironmentInstance instance, string mapEventId, string expectedHost, params string[] expectedSuccessors)
    {
        instance.Call(() =>
        {
            var registry = instance.Resolve<IBattleHostRegistry>();
            Assert.True(registry.TryGet(mapEventId, out var assignment), $"No host assignment on {instance.GetType().Name}");
            Assert.Equal(expectedHost, assignment.HostControllerId);
            Assert.Equal(expectedSuccessors, assignment.SuccessorControllerIds.ToArray());
        });
    }

    /// <summary>Asserts whether <paramref name="instance"/> considers itself the host of the battle.</summary>
    protected void AssertIsLocalHost(EnvironmentInstance instance, string mapEventId, bool expected)
    {
        instance.Call(() =>
        {
            var registry = instance.Resolve<IBattleHostRegistry>();
            Assert.Equal(expected, registry.IsLocalHost(mapEventId));
        });
    }
}
