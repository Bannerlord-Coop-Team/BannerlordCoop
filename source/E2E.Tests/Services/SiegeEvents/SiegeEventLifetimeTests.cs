using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeEventLifetimeTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    public SiegeEventLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase>
        {
            AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        };
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerStartSiegeEvent_MissedGraphWrites_RepairedByInitializationSnapshot()
    {
        // Arrange
        foreach (var instance in AllEnvironmentInstances)
        {
            instance.Call(EnsureTestPreparationsType);
        }

        string? siegeEventId = null;
        string? settlementId = null;
        string? besiegerCampId = null;
        string? besiegerPartyId = null;
        string? supportPartyId = null;
        string? preparationsId = null;
        long siegeStartTimeTicks = 0;

        // Act
        using (new SiegeGraphAutoSyncBlocker())
        using (new SiegeSideInitializationStub())
        {
            Server.Call(() =>
            {
                var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
                var besiegerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
                Campaign.Current.SiegeEventManager.StartSiegeEvent(settlement, besiegerParty);

                var siegeEvent = settlement.SiegeEvent;

                Assert.True(Server.ObjectManager.TryGetId(siegeEvent, out siegeEventId));
                Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));
                Assert.True(Server.ObjectManager.TryGetId(siegeEvent.BesiegerCamp, out besiegerCampId));
                Assert.True(Server.ObjectManager.TryGetId(besiegerParty, out besiegerPartyId));
                Assert.True(Server.ObjectManager.TryGetId(
                    siegeEvent.BesiegerCamp.SiegeEngines.SiegePreparations, out preparationsId));
                siegeStartTimeTicks = siegeEvent.SiegeStartTime.NumTicks;

                var supportParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
                supportParty._besiegerCamp = siegeEvent.BesiegerCamp;
                siegeEvent.BesiegerCamp._besiegerParties.Add(supportParty);
                Assert.True(Server.ObjectManager.TryGetId(supportParty, out supportPartyId));
                Assert.True(Server.Resolve<ISiegeEventGraphSynchronizer>().TryCapture(
                    siegeEvent, out var snapshot, besiegerParty));
                Server.Resolve<INetwork>().SendAll(new NetworkInitializeSiegeEvent(snapshot));
            }, disabledMethods);
        }

        // Assert
        Assert.NotNull(siegeEventId);
        Assert.NotNull(settlementId);
        Assert.NotNull(besiegerCampId);
        Assert.NotNull(besiegerPartyId);
        Assert.NotNull(supportPartyId);
        Assert.NotNull(preparationsId);
        Assert.Contains(Server.NetworkSentMessages,
            message => message.GetType().Name == "SiegeEvent_BesiegedSettlement_SetNetworkMessage");
        Assert.Contains(Server.NetworkSentMessages,
            message => message.GetType().Name == "SiegeEvent_BesiegerCamp_SetNetworkMessage");
        Assert.Contains(Server.NetworkSentMessages,
            message => message.GetType().Name == "Settlement_SiegeEvent_SetNetworkMessage");
        var initializations = Server.NetworkSentMessages.GetMessages<NetworkInitializeSiegeEvent>().ToArray();
        Assert.Equal(2, initializations.Length);
        var initialization = initializations[^1];
        Assert.NotNull(initialization.BesiegerPartyIds);
        Assert.Contains(besiegerPartyId, initialization.BesiegerPartyIds);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var besiegerCamp));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var besiegerParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(supportPartyId, out var supportParty));
            Assert.Same(siegeEvent, settlement.SiegeEvent);
            Assert.Same(settlement, siegeEvent.BesiegedSettlement);
            Assert.Same(besiegerCamp, siegeEvent.BesiegerCamp);
            Assert.Same(besiegerParty, besiegerCamp.LeaderParty);
            Assert.Same(besiegerCamp, besiegerParty.BesiegerCamp);
            Assert.Same(besiegerCamp, supportParty.BesiegerCamp);
            Assert.Equal(1, besiegerCamp._besiegerParties.Count(party => party == besiegerParty));
            Assert.Equal(1, besiegerCamp._besiegerParties.Count(party => party == supportParty));
            Assert.NotNull(besiegerCamp.SiegeEngines?.DeployedRangedSiegeEngines);
            Assert.NotNull(settlement.SiegeEngines?.DeployedRangedSiegeEngines);
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent.SiegeEngineConstructionProgress>(
                preparationsId, out var preparations));
            Assert.Same(preparations, besiegerCamp.SiegeEngines.SiegePreparations);
            Assert.Equal(0.75f, preparations.Progress);
            Assert.Equal(1f, preparations.RedeploymentProgress);
            Assert.Equal(100f, preparations.Hitpoints);
            Assert.Equal(100f, preparations.MaxHitPoints);
            Assert.Equal(siegeStartTimeTicks, siegeEvent.SiegeStartTime.NumTicks);

            client.Call(() =>
            {
                var leaderPosition = new CampaignVec2(new Vec2(17f, 23f), true);
                var supportPosition = new CampaignVec2(new Vec2(31f, 47f), true);
                var stalePosition = new CampaignVec2(new Vec2(53f, 71f), true);
                var staleParty = ObjectHelper.SkipConstructor<MobileParty>();
                besiegerParty._position = leaderPosition;
                supportParty._position = supportPosition;
                staleParty._position = stalePosition;
                besiegerParty._besiegerCamp = null;
                supportParty._besiegerCamp = null;
                staleParty._besiegerCamp = besiegerCamp;
                besiegerCamp._besiegerParties.Clear();
                besiegerCamp._besiegerParties.Add(staleParty);

                Assert.True(client.Resolve<ISiegeEventGraphSynchronizer>().TryApply(
                    initialization.ToSnapshot()));
                Assert.Equal(leaderPosition, besiegerParty.Position);
                Assert.Equal(supportPosition, supportParty.Position);
                Assert.Equal(stalePosition, staleParty.Position);
                Assert.Same(besiegerCamp, besiegerParty.BesiegerCamp);
                Assert.Same(besiegerCamp, supportParty.BesiegerCamp);
                Assert.Null(staleParty.BesiegerCamp);
                Assert.DoesNotContain(staleParty, besiegerCamp._besiegerParties);

                long retainedStartTimeTicks = siegeStartTimeTicks + 10;
                using (new AllowedThread())
                {
                    siegeEvent.SiegeStartTime = new CampaignTime(retainedStartTimeTicks);
                    besiegerCamp.NumberOfTroopsKilledOnSide = 9;
                }

                var legacySnapshot = new SiegeEventGraphSnapshot(
                    siegeEventId,
                    settlementId,
                    besiegerCampId,
                    besiegerPartyId,
                    initialization.AttackerSiegeEnginesId,
                    initialization.DefenderSiegeEnginesId);
                Assert.True(client.Resolve<ISiegeEventGraphSynchronizer>().TryApply(legacySnapshot));
                Assert.Equal(retainedStartTimeTicks, siegeEvent.SiegeStartTime.NumTicks);
                Assert.Equal(9, besiegerCamp.NumberOfTroopsKilledOnSide);
                Assert.Contains(besiegerParty, besiegerCamp._besiegerParties);
                Assert.Same(preparations, besiegerCamp.SiegeEngines.SiegePreparations);
            });
        }
    }

    [Fact]
    public void ClientCreateSiegeEvent_DoesNothing()
    {
        // Arrange
        string? clientSiegeEventId = null;

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            var SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();

            Assert.False(firstClient.ObjectManager.TryGetId(SiegeEvent, out clientSiegeEventId));
        });

        // Assert
        Assert.Null(clientSiegeEventId);
    }

    [Fact]
    public void SiegeGraphMessages_RoundTripAllIds()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var snapshot = new SiegeEventGraphSnapshot(
            "siege-event",
            "settlement",
            "camp",
            "leader",
            "attacker-engines",
            "defender-engines",
            1234,
            "strategy",
            7,
            new[] { "leader", "support" },
            new[]
            {
                new SiegeEngineGraphSnapshot(
                    "preparation", "preparations", 0.75f, 1f, 100f, 100f,
                    SiegeEngineGraphLocation.Preparation),
            },
            Array.Empty<SiegeEngineGraphSnapshot>());

        var initialization = RoundTrip(serializer, new NetworkInitializeSiegeEvent(snapshot));
        AssertGraph(snapshot, initialization.ToSnapshot());

        var mapCommit = RoundTrip(serializer, new NetworkMapEventInitialized(
            "map-event", false, "tracker", "component", "visual", snapshot));
        AssertGraph(snapshot, mapCommit.SiegeGraph);
    }

    private static T RoundTrip<T>(ProtoBufSerializer serializer, T message)
    {
        return serializer.Deserialize<T>(serializer.Serialize(message));
    }

    private static void AssertGraph(SiegeEventGraphSnapshot expected, SiegeEventGraphSnapshot actual)
    {
        Assert.Equal(expected.SiegeEventId, actual.SiegeEventId);
        Assert.Equal(expected.SettlementId, actual.SettlementId);
        Assert.Equal(expected.BesiegerCampId, actual.BesiegerCampId);
        Assert.Equal(expected.LeaderPartyId, actual.LeaderPartyId);
        Assert.Equal(expected.AttackerSiegeEnginesId, actual.AttackerSiegeEnginesId);
        Assert.Equal(expected.DefenderSiegeEnginesId, actual.DefenderSiegeEnginesId);
        Assert.Equal(expected.SiegeStartTimeTicks, actual.SiegeStartTimeTicks);
        Assert.Equal(expected.BesiegerStrategyId, actual.BesiegerStrategyId);
        Assert.Equal(expected.BesiegerTroopsKilled, actual.BesiegerTroopsKilled);
        Assert.Equal(expected.BesiegerPartyIds, actual.BesiegerPartyIds);
        Assert.Equal(expected.AttackerEngines, actual.AttackerEngines);
        Assert.Equal(expected.DefenderEngines ?? Array.Empty<SiegeEngineGraphSnapshot>(),
            actual.DefenderEngines ?? Array.Empty<SiegeEngineGraphSnapshot>());
    }

    private static void EnsureTestPreparationsType()
    {
        const string preparationsId = "issue_3253_test_preparations";
        if (MBObjectManager.Instance.GetObject<SiegeEngineType>(preparationsId) != null) return;

        var preparationsType = ObjectHelper.SkipConstructor<SiegeEngineType>();
        using (new AllowedThread()) preparationsType.StringId = preparationsId;
        MBObjectManager.Instance.RegisterObject(preparationsType);
    }

    private sealed class SiegeGraphAutoSyncBlocker : IDisposable
    {
        private static readonly MethodInfo[] DeliveryMethods =
        {
            AccessTools.Method(
                typeof(TestNetworkRouter),
                nameof(TestNetworkRouter.Send),
                new[] { typeof(LiteNetLib.NetPeer), typeof(LiteNetLib.NetPeer), typeof(IMessage) }),
            AccessTools.Method(
                typeof(TestNetworkRouter),
                nameof(TestNetworkRouter.SendAll),
                new[] { typeof(LiteNetLib.NetPeer), typeof(IMessage) }),
        };

        private static readonly HashSet<string> BlockedMessages = new()
        {
            "Settlement_SiegeEvent_SetNetworkMessage",
            "Settlement_SiegeEngines_SetNetworkMessage",
            "SiegeEvent_BesiegedSettlement_SetNetworkMessage",
            "SiegeEvent_BesiegerCamp_SetNetworkMessage",
            "BesiegerCamp_SiegeEvent_SetNetworkMessage",
            "BesiegerCamp__leaderParty_SetNetworkMessage",
            "BesiegerCamp_SiegeEngines_SetNetworkMessage",
            "MobileParty_BesiegerCamp_SetNetworkMessage",
        };

        private readonly Harmony harmony = new($"siege-graph-network-blocker-{Guid.NewGuid()}");

        public SiegeGraphAutoSyncBlocker()
        {
            var prefix = new HarmonyMethod(typeof(SiegeGraphAutoSyncBlocker), nameof(AllowDelivery));
            foreach (var method in DeliveryMethods)
            {
                harmony.Patch(method, prefix: prefix);
            }
        }

        public void Dispose()
        {
            foreach (var method in DeliveryMethods)
            {
                harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
            }
        }

        private static bool AllowDelivery(IMessage message) =>
            !BlockedMessages.Contains(message.GetType().Name) && !IsBlockedCreate(message.GetType());

        private static bool IsBlockedCreate(Type messageType)
        {
            if (!messageType.IsGenericType ||
                messageType.GetGenericTypeDefinition() != typeof(NetworkCreateInstance<>)) return false;

            var instanceType = messageType.GetGenericArguments()[0];
            return instanceType == typeof(SiegeEvent) ||
                   instanceType == typeof(BesiegerCamp) ||
                   instanceType == typeof(SiegeEvent.SiegeEnginesContainer) ||
                   instanceType == typeof(SiegeEvent.SiegeEngineConstructionProgress);
        }
    }

    private sealed class SiegeSideInitializationStub : IDisposable
    {
        private static readonly MethodInfo BesiegerInitialization =
            AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide));
        private static readonly MethodInfo SettlementInitialization =
            AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide));

        private readonly Harmony harmony = new($"siege-side-initialization-stub-{Guid.NewGuid()}");

        public SiegeSideInitializationStub()
        {
            harmony.Patch(BesiegerInitialization,
                prefix: new HarmonyMethod(typeof(SiegeSideInitializationStub), nameof(InitializeBesieger)));
            harmony.Patch(SettlementInitialization,
                prefix: new HarmonyMethod(typeof(SiegeSideInitializationStub), nameof(InitializeDefender)));
        }

        public void Dispose()
        {
            harmony.Unpatch(BesiegerInitialization, HarmonyPatchType.Prefix, harmony.Id);
            harmony.Unpatch(SettlementInitialization, HarmonyPatchType.Prefix, harmony.Id);
        }

        private static bool InitializeBesieger(BesiegerCamp __instance)
        {
            const string preparationsId = "issue_3253_test_preparations";
            var preparationsType = MBObjectManager.Instance.GetObject<SiegeEngineType>(preparationsId);

            var preparations = new SiegeEvent.SiegeEngineConstructionProgress(
                preparationsType, 0.75f, 100f);
            __instance.SiegeEngines = new SiegeEvent.SiegeEnginesContainer(
                BattleSideEnum.Attacker, preparations);
            return false;
        }

        private static bool InitializeDefender(Settlement __instance)
        {
            __instance.SiegeEngines = new SiegeEvent.SiegeEnginesContainer(
                BattleSideEnum.Defender, null);
            return false;
        }
    }
}
