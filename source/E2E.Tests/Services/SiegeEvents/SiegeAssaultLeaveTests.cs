using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeAssaultLeaveTests : MapEventTestBase
{
    public SiegeAssaultLeaveTests(ITestOutputHelper output) : base(output)
    {
    }

    private static List<MethodBase> SiegeCreationDisabledMethods => new()
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
    };

    [Fact]
    public void ActiveSiegeAttacker_WithoutServerCamp_CanLeave()
    {
        var mapEvent = CreateServerMapEvent();
        var partyId = JoinNewServerPartyToSide(mapEvent.MapEventId, BattleSideEnum.Attacker);
        SetMapEventType(mapEvent.MapEventId, MapEvent.BattleTypes.Siege);
        var leavingClient = Clients.First();
        SetMainParty(leavingClient, partyId);
        SetClientOnlyCamp(leavingClient, partyId);
        string? partyBaseId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });

        Assert.NotNull(partyBaseId);
        AssertPartyState(Server, partyId, expectMapEvent: true, expectCamp: false);
        AssertPartyState(leavingClient, partyId, expectMapEvent: true, expectCamp: true);
        Server.NetworkSentMessages.Clear();

        leavingClient.Call(() =>
        {
            leavingClient.Resolve<INetwork>().SendAll(new NetworkRequestBreakSiege(partyId));
        }, MapEventDisabledMethods
            .Concat(SiegeCreationDisabledMethods)
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList());

        var left = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
        Assert.Equal(partyBaseId, left.PartyId);
        Assert.True(left.LeaveSiege);

        var approval = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
        Assert.True(approval.Approved);
        Assert.True(approval.BattleLeaveApplied);

        AssertPartyState(Server, partyId, expectMapEvent: false, expectCamp: false);
        foreach (var client in Clients)
        {
            AssertPartyState(client, partyId, expectMapEvent: false, expectCamp: false);
        }
    }

    [Fact]
    public void WoundedNonInitiator_WhenSiegeMissionStarts_LeavesSiege()
    {
        var mapEventContext = CreateServerMapEvent();
        var (_, initiatingPartyId) = CreatePlayerHeroParty("InitiatingPlayer");
        var (woundedHeroId, woundedPartyId) = CreatePlayerHeroParty("WoundedPlayer");
        var woundedClient = Clients.Last();
        string? woundedPartyBaseId = null;

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(PartyBaseHelper), nameof(PartyBaseHelper.HasFeat)))
            .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyLeftSiegeInternal)))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(initiatingPartyId, out var initiatingParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(woundedPartyId, out var woundedParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(woundedHeroId, out var woundedHero));

            initiatingParty.Party.MapEventSide = mapEvent.AttackerSide;
            woundedParty.Party.MapEventSide = mapEvent.AttackerSide;
            woundedHero.HitPoints = 1;
            Assert.True(woundedHero.IsWounded);
            Assert.True(Server.ObjectManager.TryGetId(woundedParty.Party, out woundedPartyBaseId));
        }, disabledMethods);

        SetMapEventType(mapEventContext.MapEventId, MapEvent.BattleTypes.Siege);
        SetMainParty(woundedClient, woundedPartyId);
        var siegeEventId = SetClientOnlyCamp(woundedClient, woundedPartyId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(woundedPartyId, out var woundedParty));
            woundedParty._besiegerCamp = siegeEvent.BesiegerCamp;
        });
        woundedClient.Call(() =>
        {
            PlayerEncounter.Start();
            PlayerEncounter.Init();
            Assert.NotNull(PlayerSiege.PlayerSiegeEvent);
            Assert.NotNull(PlayerEncounter.Current);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            using var handler = new BattleMissionStartHandler(
                Server.Resolve<IMessageBroker>(),
                Server.ObjectManager,
                Server.Resolve<IPlayerManager>(),
                Server.Resolve<INetwork>(),
                Server.Resolve<IMapEventLogger>(),
                Server.Resolve<IBattleMissionInitializerResolver>());

            Assert.True(handler.RemoveWoundedNonInitiatorParties(mapEvent, initiatingPartyId));
        }, disabledMethods);

        Assert.NotNull(woundedPartyBaseId);
        var left = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
        Assert.Equal(woundedPartyBaseId, left.PartyId);
        Assert.True(left.LeaveSiege);

        AssertPartyState(Server, woundedPartyId, expectMapEvent: false, expectCamp: false);
        foreach (var client in Clients)
        {
            AssertPartyState(client, woundedPartyId, expectMapEvent: false, expectCamp: false);
        }
        woundedClient.Call(() =>
        {
            Assert.Null(PlayerSiege.PlayerSiegeEvent);
            Assert.Null(PlayerEncounter.Current);
            Assert.Equal(AiBehavior.Hold, MobileParty.MainParty.DefaultBehavior);
        });
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.FieldBattle, BattleSideEnum.Attacker)]
    [InlineData(MapEvent.BattleTypes.Siege, BattleSideEnum.Defender)]
    public void WoundedNonInitiator_OutsideAttackingSiege_DoesNotLeaveSiege(
        MapEvent.BattleTypes battleType,
        BattleSideEnum woundedSide)
    {
        var mapEventContext = CreateServerMapEvent();
        var (_, initiatingPartyId) = CreatePlayerHeroParty("InitiatingPlayer");
        var (woundedHeroId, woundedPartyId) = CreatePlayerHeroParty("WoundedPlayer");

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(PartyBaseHelper), nameof(PartyBaseHelper.HasFeat)))
            .ToList();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(initiatingPartyId, out var initiatingParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(woundedPartyId, out var woundedParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(woundedHeroId, out var woundedHero));

            initiatingParty.Party.MapEventSide = mapEvent.AttackerSide;
            woundedParty.Party.MapEventSide = mapEvent.GetMapEventSide(woundedSide);
            woundedHero.HitPoints = 1;
            Assert.True(woundedHero.IsWounded);
        }, disabledMethods);
        SetMapEventType(mapEventContext.MapEventId, battleType);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            using var handler = new BattleMissionStartHandler(
                Server.Resolve<IMessageBroker>(),
                Server.ObjectManager,
                Server.Resolve<IPlayerManager>(),
                Server.Resolve<INetwork>(),
                Server.Resolve<IMapEventLogger>(),
                Server.Resolve<IBattleMissionInitializerResolver>());

            Assert.True(handler.RemoveWoundedNonInitiatorParties(mapEvent, initiatingPartyId));
        }, disabledMethods);

        var left = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
        Assert.False(left.LeaveSiege);
        AssertPartyState(Server, woundedPartyId, expectMapEvent: false, expectCamp: false);
        foreach (var client in Clients)
        {
            AssertPartyState(client, woundedPartyId, expectMapEvent: false, expectCamp: false);
        }
    }

    private void SetMapEventType(string mapEventId, MapEvent.BattleTypes battleType)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
                mapEvent._mapEventType = battleType;
            });
        }
    }

    private string SetClientOnlyCamp(EnvironmentInstance client, string partyId)
    {
        var siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(SiegeCreationDisabledMethods);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.NotNull(siegeEvent.BesiegerCamp);

            party._besiegerCamp = siegeEvent.BesiegerCamp;
        });
        return siegeEventId;
    }

    private static void SetMainParty(EnvironmentInstance instance, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
        });
    }

    private static void AssertPartyState(
        EnvironmentInstance instance,
        string partyId,
        bool expectMapEvent,
        bool expectCamp)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Equal(expectMapEvent, party.MapEvent != null);
            Assert.Equal(expectCamp, party.BesiegerCamp != null);
        });
    }
}
