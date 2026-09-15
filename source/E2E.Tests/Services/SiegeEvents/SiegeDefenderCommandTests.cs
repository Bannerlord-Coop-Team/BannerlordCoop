using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// Two co-op defenders inside one siege: the defender side stays on Custom so AI never plans
/// over player builds.
/// </summary>
public class SiegeDefenderCommandTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    public SiegeDefenderCommandTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private static List<MethodBase> SiegeCreationDisabledMethods => new()
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
    };

    [Fact]
    public void DefenderStrategy_WithPlayerDefendersInside_ResolvesCustom()
    {
        var siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(SiegeCreationDisabledMethods);
        var firstDefenderId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var secondDefenderId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        WireDefendersInside(siegeEventId, firstDefenderId, secondDefenderId);
        RegisterDefendersAsPlayers(firstDefenderId, secondDefenderId);
        AttachTown(siegeEventId);
        EnsureEncounterModel();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            var prefix = AccessTools.Method(typeof(SiegeEventCampaignBehaviorPatches), "SetDefaultTacticsPrefix");
            Assert.NotNull(prefix);

            bool runOriginal = (bool)prefix.Invoke(null, new object[] { siegeEvent, BattleSideEnum.Defender });

            Assert.False(runOriginal);
            Assert.Same(DefaultSiegeStrategies.Custom, siegeEvent.GetSiegeEventSide(BattleSideEnum.Defender).SiegeStrategy);
        });
    }

    private void WireDefendersInside(string siegeEventId, params string[] defenderPartyIds)
    {
        foreach (var instance in AllEnvironmentInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
                foreach (var partyId in defenderPartyIds)
                {
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var defender));
                    defender._currentSettlement = siegeEvent.BesiegedSettlement;
                    if (!siegeEvent.BesiegedSettlement._partiesCache.Contains(defender))
                        siegeEvent.BesiegedSettlement._partiesCache.Add(defender);
                }
            });
        }
    }

    private void RegisterDefendersAsPlayers(params string[] defenderPartyIds)
    {
        int index = 0;
        foreach (var partyId in defenderPartyIds)
        {
            string controllerId = $"DefenderCommand{index++}";
            foreach (var instance in AllEnvironmentInstances)
            {
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                    Assert.NotNull(party.LeaderHero);
                    Assert.True(instance.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
                    Assert.True(instance.ObjectManager.TryGetId(party.LeaderHero.CharacterObject, out var characterId));
                    string clanId = string.Empty;
                    if (party.ActualClan != null)
                        instance.ObjectManager.TryGetId(party.ActualClan, out clanId);

                    Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(new Player(
                        controllerId,
                        heroId,
                        partyId,
                        clanId,
                        characterId)));
                });
            }
        }
    }

    private void AttachTown(string siegeEventId)
    {
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        foreach (var instance in AllEnvironmentInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                town.Owner = siegeEvent.BesiegedSettlement.Party;
                siegeEvent.BesiegedSettlement.Town = town;
            });
        }
    }

    private void EnsureEncounterModel()
    {
        foreach (var instance in AllEnvironmentInstances)
        {
            instance.Call(() =>
            {
                if (Campaign.Current.Models.EncounterModel != null) return;

                var models = Campaign.Current.Models.GetGameModels().ToList();
                models.Add(new TaleWorlds.CampaignSystem.GameComponents.DefaultEncounterModel());
                var gameModels = new TaleWorlds.CampaignSystem.GameModels(models);
                instance.GameInstance.Game._gameModelManagers[typeof(TaleWorlds.CampaignSystem.GameModels)] = gameModels;
                Campaign.Current._gameModels = gameModels;
            });
        }
    }
}
