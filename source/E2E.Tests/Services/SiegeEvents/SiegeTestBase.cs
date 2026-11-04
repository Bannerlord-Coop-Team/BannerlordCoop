using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// Shared setup for siege tests: a registered siege whose besieger camp holds the given parties on every instance,
/// the models siege teardown needs, and a player disconnect.
/// </summary>
public abstract class SiegeTestBase : MapEventTestBase
{
    protected static IReadOnlyList<MethodBase> SiegeCreationDisabledMethods => new[]
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
    };

    protected SiegeTestBase(ITestOutputHelper output) : base(output)
    {
    }

    protected SiegeContext SetupSiege(params string[] attackerPartyIds)
    {
        var siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(SiegeCreationDisabledMethods);
        ConfigureSiegeTeardownModels();
        string? campId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            Assert.True(Server.ObjectManager.TryGetId(siegeEvent.BesiegerCamp, out campId));
        });
        Assert.NotNull(campId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<BesiegerCamp>(campId!, out var camp));
                camp._besiegerParties.Clear();

                foreach (var partyId in attackerPartyIds)
                {
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                    if (party.Ai == null)
                    {
                        using (new AllowedThread())
                        {
                            party.Ai = new MobilePartyAi(party);
                        }
                    }

                    party._besiegerCamp = camp;
                    camp._besiegerParties.Add(party);
                }

                camp._leaderParty = camp._besiegerParties[0];
                camp._faction = camp._leaderParty.MapFaction;
            });
        }

        return new SiegeContext(siegeEventId, campId!);
    }

    protected void ConfigureSiegeTeardownModels()
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var models = Campaign.Current.Models.GetGameModels().ToList();
                models.Add(new DefaultEncounterModel());
                models.Add(new DefaultPartyImpairmentModel());
                models.Add(new DefaultPartyNavigationModel());
                var gameModels = new GameModels(models);

                instance.GameInstance.Game._gameModelManagers[typeof(GameModels)] = gameModels;
                Campaign.Current._gameModels = gameModels;
            });
        }
    }

    protected void DisconnectPlayer(
        EnvironmentInstance disconnectedClient,
        string controllerId)
    {
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer(controllerId, disconnectedClient.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(
                this,
                new PlayerDisconnected(disconnectedClient.NetPeer, default));
        });
    }

    protected readonly record struct SiegeContext(string SiegeEventId, string CampId);
}
