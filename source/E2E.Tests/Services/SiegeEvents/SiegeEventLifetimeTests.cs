using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
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
        string? siegeEventId = null;
        string? settlementId = null;
        string? besiegerCampId = null;
        string? besiegerPartyId = null;

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
            }, disabledMethods);
        }

        // Assert
        Assert.NotNull(siegeEventId);
        Assert.NotNull(settlementId);
        Assert.NotNull(besiegerCampId);
        Assert.NotNull(besiegerPartyId);
        Assert.Contains(Server.NetworkSentMessages,
            message => message.GetType().Name == "SiegeEvent_BesiegedSettlement_SetNetworkMessage");
        Assert.Contains(Server.NetworkSentMessages,
            message => message.GetType().Name == "SiegeEvent_BesiegerCamp_SetNetworkMessage");
        Assert.Contains(Server.NetworkSentMessages,
            message => message.GetType().Name == "Settlement_SiegeEvent_SetNetworkMessage");
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkInitializeSiegeEvent>());

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var besiegerCamp));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var besiegerParty));
            Assert.Same(siegeEvent, settlement.SiegeEvent);
            Assert.Same(settlement, siegeEvent.BesiegedSettlement);
            Assert.Same(besiegerCamp, siegeEvent.BesiegerCamp);
            Assert.Same(besiegerParty, besiegerCamp.LeaderParty);
            Assert.Same(besiegerCamp, besiegerParty.BesiegerCamp);
            Assert.NotNull(besiegerCamp.SiegeEngines?.DeployedRangedSiegeEngines);
            Assert.NotNull(settlement.SiegeEngines?.DeployedRangedSiegeEngines);
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
            !BlockedMessages.Contains(message.GetType().Name);
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
            __instance.SiegeEngines =
                GameObjectCreator.CreateInitializedObject<SiegeEvent.SiegeEnginesContainer>();
            return false;
        }

        private static bool InitializeDefender(Settlement __instance)
        {
            __instance.SiegeEngines =
                GameObjectCreator.CreateInitializedObject<SiegeEvent.SiegeEnginesContainer>();
            return false;
        }
    }
}
