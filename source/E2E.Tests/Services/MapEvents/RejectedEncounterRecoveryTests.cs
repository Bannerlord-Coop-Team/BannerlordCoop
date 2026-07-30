using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using Helpers;
using LiteNetLib;
using Moq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class RejectedEncounterRecoveryTests : MapEventTestBase
{
    private static readonly MethodInfo StartBattleInternal =
        AccessTools.Method(typeof(PlayerEncounter), "StartBattleInternal");

    public RejectedEncounterRecoveryTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ServerRejectedFieldEncounter_ReturnsPendingEncounterToMap(bool isLooterParty)
    {
        var client = Clients.First();
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var targetPartyId = CreateEncounterTarget(isLooterParty);
        RegisterPeer(client, "PlayerOne");
        MakePartiesHostile(playerPartyId, targetPartyId);
        EnableHeadlessEncounterFinish(client);
        var authoritativePosition = new CampaignVec2(new Vec2(42f, 24f), true);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(targetPartyId, out var targetParty));
            using (new AllowedThread())
            {
                targetParty.IsActive = false;
            }
        }, MapEventDisabledMethods);

        var pendingEncounter = SetupPendingEncounter(
            client,
            playerPartyId,
            targetPartyId,
            authoritativePosition);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(targetPartyId, out var targetParty));
            Assert.Equal(isLooterParty, targetParty.PartyComponent is BanditPartyComponent);
            Assert.Equal(!isLooterParty, targetParty.PartyComponent is CustomPartyComponent);

            var result = (MapEvent?)StartBattleInternal.Invoke(pendingEncounter, Array.Empty<object>());

            Assert.Null(result);
            Assert.Same(pendingEncounter, PlayerEncounter.Current);
            Assert.False(PlayerEncounter.LeaveEncounter);
        }, MapEventDisabledMethods);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(targetPartyId, out var targetParty));
            using (new AllowedThread())
            {
                targetParty.IsActive = true;
            }
        }, MapEventDisabledMethods);

        client.Call(() =>
        {
            var retryResult = (MapEvent?)StartBattleInternal.Invoke(pendingEncounter, Array.Empty<object>());

            Assert.Null(retryResult);
            Assert.Null(pendingEncounter._mapEvent);
            Assert.Null(MobileParty.MainParty.MapEvent);
        }, MapEventDisabledMethods);

        client.Call(() => GameThread.Instance.Update(TimeSpan.Zero), MapEventDisabledMethods);

        client.Call(() =>
        {
            Assert.Null(PlayerEncounter.Current);
            Assert.Null(MobileParty.MainParty.MapEvent);
            Assert.Equal(authoritativePosition, MobileParty.MainParty.Position);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void SuccessfulFieldEncounter_UsesAuthoritativeMapEvent()
    {
        var client = Clients.First();
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var targetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        RegisterPeer(client, "PlayerOne");
        MakePartiesHostile(playerPartyId, targetPartyId);
        var pendingEncounter = SetupPendingEncounter(
            client,
            playerPartyId,
            targetPartyId,
            new CampaignVec2(new Vec2(10f, 12f), true));

        MapEvent? result = null;
        client.Call(() =>
        {
            result = (MapEvent?)StartBattleInternal.Invoke(pendingEncounter, Array.Empty<object>());

            Assert.NotNull(result);
            Assert.Same(result, pendingEncounter._mapEvent);
            Assert.Same(result, MobileParty.MainParty.MapEvent);
        }, MapEventDisabledMethods);

        string? mapEventId = null;
        client.Call(() => Assert.True(client.ObjectManager.TryGetId(result, out mapEventId)));
        Assert.NotNull(mapEventId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var authoritativeMapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.Same(authoritativeMapEvent, playerParty.MapEvent);
        });
    }

    [Fact]
    public void ServerRejectedFieldEncounter_AuthoritativeEventAttachedBeforeRecoveryIsPreserved()
    {
        var client = Clients.First();
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var targetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        RegisterPeer(client, "PlayerOne");
        var pendingEncounter = SetupPendingEncounter(
            client,
            playerPartyId,
            targetPartyId,
            new CampaignVec2(new Vec2(16f, 18f), true));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(targetPartyId, out var targetParty));
            using (new AllowedThread())
            {
                targetParty.IsActive = false;
            }
        }, MapEventDisabledMethods);

        client.Call(() =>
        {
            var rejectedResult = (MapEvent?)StartBattleInternal.Invoke(
                pendingEncounter,
                Array.Empty<object>());
            Assert.Null(rejectedResult);

            var authoritativeMapEvent = ObjectHelper.SkipConstructor<MapEvent>();
            pendingEncounter._mapEvent = authoritativeMapEvent;

            var retryResult = (MapEvent?)StartBattleInternal.Invoke(
                pendingEncounter,
                Array.Empty<object>());
            Assert.Same(authoritativeMapEvent, retryResult);

            GameThread.Instance.Update(TimeSpan.Zero);

            Assert.Same(pendingEncounter, PlayerEncounter.Current);
            Assert.False(PlayerEncounter.LeaveEncounter);
            Assert.Same(authoritativeMapEvent, pendingEncounter._mapEvent);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void ServerUnresolvedFieldEncounter_KeepsEncounterForLaterReconciliation()
    {
        var client = Clients.First();
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var targetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        RegisterPeer(client, "PlayerOne");

        var pendingEncounter = SetupPendingEncounter(
            client,
            playerPartyId,
            targetPartyId,
            new CampaignVec2(new Vec2(18f, 20f), true));

        var serverBroker = Server.Resolve<IMessageBroker>();
        Action<MessagePayload<NetworkRequestCreateMapEvent>> replyUnresolved = payload =>
        {
            var requestingPeer = Assert.IsType<NetPeer>(payload.Who);
            Server.Resolve<INetwork>().Send(
                requestingPeer,
                new NetworkMapEventCreated(
                    payload.What.RequestId,
                    MapEventCreationOutcome.Unresolved,
                    null));
        };
        serverBroker.Subscribe(replyUnresolved);

        try
        {
            var disabledMethods = MapEventDisabledMethods
                .Append(AccessTools.Method(
                    typeof(MapEventCreationCoordinator),
                    "CreateAndReplyToMapEventRequest"))
                .ToList();

            client.Call(() =>
            {
                var result = (MapEvent?)StartBattleInternal.Invoke(pendingEncounter, Array.Empty<object>());
                Assert.Null(result);
            }, disabledMethods);

            client.Call(() => GameThread.Instance.Update(TimeSpan.Zero), MapEventDisabledMethods);
            client.Call(() => Assert.Same(pendingEncounter, PlayerEncounter.Current), MapEventDisabledMethods);
        }
        finally
        {
            serverBroker.Unsubscribe(replyUnresolved);
        }
    }

    [Fact]
    public void MapEventCreationTimeout_KeepsEncounterForLaterReconciliation()
    {
        var client = Clients.First();
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var targetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        RegisterPeer(client, "PlayerOne");
        SetMapEventCreationTimeout(client, TimeSpan.FromMilliseconds(1));

        var pendingEncounter = SetupPendingEncounter(
            client,
            playerPartyId,
            targetPartyId,
            new CampaignVec2(new Vec2(20f, 22f), true));
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(
                typeof(TestNetworkRouter),
                nameof(TestNetworkRouter.SendAll),
                new[] { typeof(NetPeer), typeof(IMessage) }))
            .ToList();

        client.Call(() =>
        {
            var result = (MapEvent?)StartBattleInternal.Invoke(pendingEncounter, Array.Empty<object>());
            Assert.Null(result);
        }, disabledMethods);

        client.Call(() => GameThread.Instance.Update(TimeSpan.Zero), MapEventDisabledMethods);
        client.Call(() => Assert.Same(pendingEncounter, PlayerEncounter.Current), MapEventDisabledMethods);
    }

    [Fact]
    public void EncounterLeave_WithNoBattleReference_ClosesPendingEncounter()
    {
        var client = Clients.First();
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var targetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        EnableHeadlessEncounterFinish(client);
        SetupPendingEncounter(
            client,
            playerPartyId,
            targetPartyId,
            new CampaignVec2(new Vec2(30f, 32f), true));

        client.Call(MenuHelper.EncounterLeaveConsequence, MapEventDisabledMethods);

        client.Call(() => Assert.Null(PlayerEncounter.Current), MapEventDisabledMethods);
    }

    [Fact]
    public void EncounterLeave_WithStaleBesiegerCampAndNoBattleReference_ClosesPendingEncounter()
    {
        var client = Clients.First();
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var targetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        EnableHeadlessEncounterFinish(client);
        SetupPendingEncounter(
            client,
            playerPartyId,
            targetPartyId,
            new CampaignVec2(new Vec2(34f, 36f), true));

        client.Call(() =>
        {
            using (new AllowedThread())
            {
                MobileParty.MainParty._besiegerCamp = new BesiegerCamp(null, null);
            }

            MenuHelper.EncounterLeaveConsequence();
        }, MapEventDisabledMethods);

        client.Call(() =>
        {
            Assert.Null(PlayerEncounter.Current);
            Assert.Null(MobileParty.MainParty.BesiegerCamp);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void EncounterLeave_WithLiveBesiegerCampAndNoBattleReference_UsesSiegeLeaveFlow()
    {
        var client = Clients.First();
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var targetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var pendingEncounter = SetupPendingEncounter(
            client,
            playerPartyId,
            targetPartyId,
            new CampaignVec2(new Vec2(38f, 40f), true));

        client.Call(() =>
        {
            var siegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
            var besiegerCamp = new BesiegerCamp(siegeEvent, null);
            using (new AllowedThread())
            {
                MobileParty.MainParty._besiegerCamp = besiegerCamp;
            }

            var prefix = AccessTools.Method(
                typeof(PlayerEncounterPatches),
                "EncounterLeaveWithoutMapEventPrefix");

            Assert.True((bool)prefix.Invoke(null, Array.Empty<object>()));
            Assert.Same(pendingEncounter, PlayerEncounter.Current);
            Assert.Same(besiegerCamp, MobileParty.MainParty.BesiegerCamp);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void EncounterLeave_WithNoBattleReferenceInsideSettlement_PreservesSettlement()
    {
        var client = Clients.First();
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var targetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        EnableHeadlessEncounterFinish(client);
        SetupPendingEncounter(
            client,
            playerPartyId,
            targetPartyId,
            new CampaignVec2(new Vec2(42f, 44f), true));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            MobileParty.MainParty._currentSettlement = settlement;
            Assert.Same(settlement, MobileParty.MainParty.CurrentSettlement);

            MenuHelper.EncounterLeaveConsequence();
        }, MapEventDisabledMethods);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.Null(PlayerEncounter.Current);
            Assert.Same(settlement, MobileParty.MainParty.CurrentSettlement);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void EncounterLeave_AllowedThread_PassesThroughToOriginal()
    {
        var client = Clients.First();
        client.Call(() =>
        {
            var prefix = AccessTools.Method(
                typeof(PlayerEncounterPatches),
                "EncounterLeaveWithoutMapEventPrefix");
            using (new AllowedThread())
            {
                Assert.True((bool)prefix.Invoke(null, Array.Empty<object>()));
            }
        }, MapEventDisabledMethods);
    }

    private PlayerEncounter SetupPendingEncounter(
        EnvironmentInstance client,
        string playerPartyId,
        string targetPartyId,
        CampaignVec2 playerPosition)
    {
        PlayerEncounter pendingEncounter = null;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(targetPartyId, out var targetParty));

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = playerParty;
                playerParty._position = playerPosition;
            }

            pendingEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            pendingEncounter._attackerParty = playerParty.Party;
            pendingEncounter._defenderParty = targetParty.Party;
            pendingEncounter._encounteredParty = targetParty.Party;
            Campaign.Current.PlayerEncounter = pendingEncounter;
        }, MapEventDisabledMethods);

        Assert.NotNull(pendingEncounter);
        return pendingEncounter;
    }

    private string CreateEncounterTarget(bool isLooterParty)
    {
        string? targetPartyId = null;
        Server.Call(() =>
        {
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            var position = new CampaignVec2(new Vec2(2f, 2f), true);

            MobileParty targetParty;
            if (isLooterParty)
            {
                var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
                targetParty = BanditPartyComponent.CreateLooterParty(
                    "RejectedEncounterLooters",
                    clan,
                    settlement,
                    isBossParty: false,
                    template,
                    position);
            }
            else
            {
                targetParty = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                    position,
                    spawnRadius: 0f,
                    homeSettlement: null,
                    new TextObject("Deserters"),
                    clan,
                    template,
                    owner: null);
            }

            Assert.True(Server.ObjectManager.TryGetId(targetParty, out targetPartyId));
        });

        Assert.NotNull(targetPartyId);
        return targetPartyId!;
    }

    private void MakePartiesHostile(string playerPartyId, string targetPartyId)
    {
        var playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var targetClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(targetPartyId, out var targetParty));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(targetClanId, out var targetClan));

            playerParty.ActualClan = playerClan;
            targetParty.ActualClan = targetClan;
            VillageHostileFactionStanceHelper.ApplyWarStance(playerClan, targetClan);
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(targetPartyId, out var targetParty));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(targetClanId, out var targetClan));
                Assert.Same(playerClan, playerParty.ActualClan);
                Assert.Same(targetClan, targetParty.ActualClan);

                using (new AllowedThread())
                {
                    VillageHostileFactionStanceHelper.ApplyWarStance(playerClan, targetClan);
                }

                Assert.True(VillageHostileFactionStanceHelper.HasWarStance(
                    playerParty.MapFaction,
                    targetParty.MapFaction));
            });
        }
    }

    private void RegisterPeer(EnvironmentInstance client, string controllerId)
    {
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        Server.Resolve<IPlayerManager>().SetPeer(controllerId, client.NetPeer);
    }

    private static void SetMapEventCreationTimeout(EnvironmentInstance instance, TimeSpan timeout)
    {
        instance.Call(() =>
        {
            var config = new Mock<INetworkConfig>();
            config.SetupGet(x => x.ObjectCreationTimeout).Returns(timeout);

            var coordinator = instance.Resolve<MapEventCreationCoordinator>();
            var configurationField = AccessTools.Field(typeof(MapEventCreationCoordinator), "configuration");
            Assert.NotNull(configurationField);
            configurationField.SetValue(coordinator, config.Object);
        });
    }
}
