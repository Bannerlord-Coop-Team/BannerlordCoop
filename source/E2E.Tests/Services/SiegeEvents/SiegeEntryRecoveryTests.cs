using Common;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.SiegeEvents.Interfaces;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeEntryRecoveryTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public SiegeEntryRecoveryTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void CampClear_WhenChildDetaches_ClearsAllOriginalMembersAndAllowsAnotherCamp()
    {
        using var callbacks = new SiegeCallbacks();
        var parentId = environment.CreateRegisteredObject<MobileParty>();
        var firstId = environment.CreateRegisteredObject<MobileParty>();
        var secondId = environment.CreateRegisteredObject<MobileParty>();
        var campId = environment.CreateRegisteredObject<BesiegerCamp>();

        foreach (var instance in environment.Clients.Append(environment.Server))
        {
            instance.Call(() =>
            {
                var parent = instance.GetRegisteredObject<MobileParty>(parentId);
                var first = instance.GetRegisteredObject<MobileParty>(firstId);
                var second = instance.GetRegisteredObject<MobileParty>(secondId);
                var camp = instance.GetRegisteredObject<BesiegerCamp>(campId);
                parent._besiegerCamp = first._besiegerCamp = second._besiegerCamp = camp;
                parent._attachedParties.Add(first);
                parent._attachedParties.Add(second);
            });
        }

        environment.Server.Call(() =>
        {
            var parent = environment.Server.GetRegisteredObject<MobileParty>(parentId);
            var first = environment.Server.GetRegisteredObject<MobileParty>(firstId);
            var second = environment.Server.GetRegisteredObject<MobileParty>(secondId);
            SiegeCallbacks.Leaving = party =>
            {
                if (ReferenceEquals(party, first)) parent._attachedParties.Remove(first);
            };

            parent.BesiegerCamp = null;

            Assert.DoesNotContain(first, parent._attachedParties);
            Assert.Contains(second, parent._attachedParties);
        });

        foreach (var instance in environment.Clients.Append(environment.Server))
        {
            instance.Call(() =>
            {
                foreach (var id in new[] { parentId, firstId, secondId })
                {
                    var party = instance.GetRegisteredObject<MobileParty>(id);
                    Assert.Null(party.BesiegerCamp);
                    Assert.False(party._besiegerCampResetStarted);
                }
            });
        }

        environment.Server.Call(() =>
        {
            var parent = environment.Server.GetRegisteredObject<MobileParty>(parentId);
            var nextCamp = new BesiegerCamp(null, parent.MapFaction);
            parent.BesiegerCamp = nextCamp;
            Assert.Same(nextCamp, parent.BesiegerCamp);
            Assert.Same(nextCamp, environment.Server.GetRegisteredObject<MobileParty>(secondId).BesiegerCamp);
            Assert.False(parent._besiegerCampResetStarted);
        });
    }

    [Fact]
    public void CampClear_WhenCallbackThrows_PropagatesFailureWithoutLatchingTheParty()
    {
        using var callbacks = new SiegeCallbacks();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var campId = environment.CreateRegisteredObject<BesiegerCamp>();
        var failure = new InvalidOperationException("injected siege callback failure");

        environment.Server.Call(() =>
        {
            var party = environment.Server.GetRegisteredObject<MobileParty>(partyId);
            party._besiegerCamp = environment.Server.GetRegisteredObject<BesiegerCamp>(campId);
            SiegeCallbacks.Leaving = _ => throw failure;

            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => party.BesiegerCamp = null));
            Assert.False(party._besiegerCampResetStarted);
        });

        Assert.DoesNotContain(environment.Server.NetworkSentMessages,
            message => message.GetType().Name == "MobileParty__besiegerCampResetStarted_SetNetworkMessage");
        foreach (var client in environment.Clients)
        {
            client.Call(() => Assert.False(client.GetRegisteredObject<MobileParty>(partyId)._besiegerCampResetStarted));
        }
    }

    [Fact]
    public void StartSiege_WhenEngineInitializationThrows_RemovesReplicatedGraphAndAllowsRetry()
    {
        using var callbacks = new SiegeCallbacks();
        SiegeCallbacks.InitializeSides = true;
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        string? failedSiegeId = null;
        string? failedCampId = null;
        string? failedContainerId = null;
        SiegeEvent? failedSiege = null;
        var failure = new InvalidOperationException("injected prebuilt engine failure");

        environment.Server.Call(() =>
        {
            var party = environment.Server.GetRegisteredObject<MobileParty>(partyId);
            var settlement = environment.Server.GetRegisteredObject<Settlement>(settlementId);
            SiegeCallbacks.Initializing = camp =>
            {
                failedSiege = camp.SiegeEvent;
                Assert.True(environment.Server.ObjectManager.TryGetId(failedSiege, out failedSiegeId));
                Assert.True(environment.Server.ObjectManager.TryGetId(camp, out failedCampId));
                Assert.True(environment.Server.ObjectManager.TryGetId(camp.SiegeEngines, out failedContainerId));
                throw failure;
            };

            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
                environment.Server.Resolve<ISiegeEventInterface>().StartSiegeEvent(party, settlement)));

            Assert.Null(settlement.SiegeEvent);
            Assert.Null(party.BesiegerCamp);
            Assert.False(party._besiegerCampResetStarted);
            Assert.DoesNotContain(failedSiege, Campaign.Current.SiegeEventManager.SiegeEvents);
        });

        Assert.NotNull(failedSiegeId);
        Assert.NotNull(failedCampId);
        Assert.NotNull(failedContainerId);
        foreach (var instance in environment.Clients.Append(environment.Server))
        {
            instance.Call(() =>
            {
                Assert.False(instance.ObjectManager.TryGetObject<SiegeEvent>(failedSiegeId, out _));
                Assert.False(instance.ObjectManager.TryGetObject<BesiegerCamp>(failedCampId, out _));
                Assert.False(instance.ObjectManager.TryGetObject<SiegeEvent.SiegeEnginesContainer>(failedContainerId, out _));
                Assert.Null(instance.GetRegisteredObject<Settlement>(settlementId).SiegeEvent);
                Assert.Null(instance.GetRegisteredObject<MobileParty>(partyId).BesiegerCamp);
            });
        }

        SiegeCallbacks.Initializing = null;
        environment.Server.Call(() =>
        {
            var party = environment.Server.GetRegisteredObject<MobileParty>(partyId);
            var settlement = environment.Server.GetRegisteredObject<Settlement>(settlementId);
            environment.Server.Resolve<ISiegeEventInterface>().StartSiegeEvent(party, settlement);

            Assert.NotNull(settlement.SiegeEvent);
            Assert.NotSame(failedSiege, settlement.SiegeEvent);
            Assert.Same(settlement.SiegeEvent.BesiegerCamp, party.BesiegerCamp);
            Assert.Contains(settlement.SiegeEvent, Campaign.Current.SiegeEventManager.SiegeEvents);
        });
    }

    // Replace scene-dependent callbacks while keeping native setters, construction and teardown live.
    private sealed class SiegeCallbacks : IDisposable
    {
        private readonly Harmony harmony = new($"siege-entry-recovery-{Guid.NewGuid()}");
        private readonly List<MethodBase> methods = new();
        public static Action<MobileParty>? Leaving;
        public static Action<BesiegerCamp>? Initializing;
        public static bool InitializeSides;

        public SiegeCallbacks()
        {
            Patch(typeof(MobileParty), nameof(MobileParty.OnPartyLeftSiegeInternal), nameof(OnLeaving));
            Patch(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal), nameof(OnJoining));
            Patch(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide), nameof(InitializeCamp));
            Patch(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide), nameof(InitializeSettlement));
        }

        private void Patch(Type type, string target, string prefix)
        {
            var method = AccessTools.Method(type, target);
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(SiegeCallbacks), prefix));
            methods.Add(method);
        }

        public void Dispose()
        {
            foreach (var method in methods) harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
            Leaving = null;
            Initializing = null;
            InitializeSides = false;
        }

        private static bool OnLeaving(MobileParty __instance)
        {
            Leaving?.Invoke(__instance);
            if (InitializeSides) __instance.BesiegerCamp._besiegerParties.Remove(__instance);
            return false;
        }

        private static bool OnJoining(MobileParty __instance)
        {
            if (InitializeSides)
            {
                __instance.BesiegerCamp._besiegerParties.Add(__instance);
                __instance.BesiegerCamp._leaderParty = __instance;
            }
            return false;
        }

        private static bool InitializeCamp(BesiegerCamp __instance)
        {
            if (InitializeSides)
            {
                __instance.SiegeEngines = new SiegeEvent.SiegeEnginesContainer(BattleSideEnum.Attacker, null);
                Initializing?.Invoke(__instance);
            }
            return false;
        }

        private static bool InitializeSettlement(Settlement __instance)
        {
            if (InitializeSides)
                __instance.SiegeEngines = new SiegeEvent.SiegeEnginesContainer(BattleSideEnum.Defender, null);
            return false;
        }
    }
}
