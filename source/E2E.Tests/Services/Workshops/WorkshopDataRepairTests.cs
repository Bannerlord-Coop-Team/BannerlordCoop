using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.CoopSessionData;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Workshops;
using GameInterface.Services.Workshops.Handlers;
using GameInterface.Services.Workshops.Messages;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Workshops;

/// <summary>
/// Saves created while MaximumWorkshopsPlayerCanHave returned the vanilla value (issue #2373)
/// contain player-owned workshops without WorkshopData: the behavior arrays were capped at the
/// vanilla size, native AddNewWorkshopData drops silently when no slot is free, and the Clan
/// screen then throws on GetOutputDailyChange for the owner. These tests cover the
/// WorkshopRepairer paths that backfill the missing data.
/// </summary>
public class WorkshopDataRepairTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public WorkshopDataRepairTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerWorkshopDataKeys_WhenOwnedWorkshopMissingBehaviorData_RestoresDataOnServerAndAllClients()
    {
        var client = Clients.First();
        var workshopId = TestEnvironment.CreateRegisteredObject<Workshop>();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var instance in Clients.Append(Server))
        {
            PreparePoisonedWorkshopState(instance, workshopId, heroId, trackInOwnedList: true);
        }

        client.Call(() => client.Resolve<Common.Network.INetwork>().SendAll(
            new NetworkInitializeServerWorkshopDataKeys(heroId)));

        AssertWorkshopDataRestored(Server, workshopId);
        foreach (var environmentClient in Clients)
        {
            AssertWorkshopDataRestored(environmentClient, workshopId);
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(workshopId, out var workshop));
            Assert.True(Server.ObjectManager.TryGetId(workshop.Settlement, out var settlementId));

            var workshopPlayerData = Server.Resolve<ICoopSessionProvider>().CoopSession.WorkshopPlayerData;
            var warehouseSlots = Assert.Contains(heroId, workshopPlayerData.PlayerWarehouseRosterPerSettlement);
            Assert.Contains(warehouseSlots, slot => slot.Key == settlementId);
        });
    }

    [Fact]
    public void ServerWorkshopDataKeys_WhenOfflinePlayerWorkshopMissingBehaviorData_RestoresDataWithoutOwnerJoining()
    {
        var client = Clients.First();
        var joiningWorkshopId = TestEnvironment.CreateRegisteredObject<Workshop>();
        var joiningHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var offlineWorkshopId = TestEnvironment.CreateRegisteredObject<Workshop>();
        var offlineHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var instance in Clients.Append(Server))
        {
            PreparePoisonedWorkshopState(instance, joiningWorkshopId, joiningHeroId, trackInOwnedList: true);
            PreparePoisonedWorkshopState(instance, offlineWorkshopId, offlineHeroId, trackInOwnedList: true);
        }

        // The offline player never announces; the server only knows them through the player
        // registry, which the saved session repopulates on load.
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().AddPlayer(
                new Player("OfflineController", offlineHeroId, null, null, null));
        });

        client.Call(() => client.Resolve<Common.Network.INetwork>().SendAll(
            new NetworkInitializeServerWorkshopDataKeys(joiningHeroId)));

        foreach (var instance in Clients.Append(Server))
        {
            AssertWorkshopDataRestored(instance, joiningWorkshopId);
            AssertWorkshopDataRestored(instance, offlineWorkshopId);
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(offlineWorkshopId, out var workshop));
            Assert.True(Server.ObjectManager.TryGetId(workshop.Settlement, out var settlementId));

            var workshopPlayerData = Server.Resolve<ICoopSessionProvider>().CoopSession.WorkshopPlayerData;
            var warehouseSlots = Assert.Contains(offlineHeroId, workshopPlayerData.PlayerWarehouseRosterPerSettlement);
            Assert.Contains(warehouseSlots, slot => slot.Key == settlementId);
        });
    }

    [Fact]
    public void RepairClientWorkshopData_WhenOwnedWorkshopMissingBehaviorData_PreparesClanFinanceWorkshopData()
    {
        var client = Clients.First();
        var workshopId = TestEnvironment.CreateRegisteredObject<Workshop>();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        PreparePoisonedWorkshopState(client, workshopId, heroId, trackInOwnedList: true);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            var workshopsBehavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
            client.Resolve<IWorkshopRepairer>().RepairClientWorkshopData(workshopsBehavior, hero);
        });

        AssertWorkshopDataRestored(client, workshopId);
    }

    [Fact]
    public void GetWorkshopsOwnedBy_WhenOwnerOnlyTrackedByWorkshopField_IncludesWorkshop()
    {
        var client = Clients.First();
        var workshopId = TestEnvironment.CreateRegisteredObject<Workshop>();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        // Workshop.Owner points at the hero but Hero._ownedWorkshops does not list the workshop —
        // the divergence a client accumulates mid-session, and exactly what the Clan screen lists.
        PreparePoisonedWorkshopState(client, workshopId, heroId, trackInOwnedList: false);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Workshop>(workshopId, out var workshop));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            var ownedWorkshops = WorkshopsCampaignBehaviorInitializationHandler.GetWorkshopsOwnedBy(hero);

            Assert.Contains(workshop, ownedWorkshops);

            var workshopsBehavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
            client.Resolve<IWorkshopRepairer>().RepairClientWorkshopData(workshopsBehavior, hero);
        });

        AssertWorkshopDataRestored(client, workshopId);
    }

    private static void PreparePoisonedWorkshopState(EnvironmentInstance instance, string workshopId, string heroId, bool trackInOwnedList)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Workshop>(workshopId, out var workshop));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            if (hero._ownedWorkshops == null)
            {
                hero._ownedWorkshops = new MBList<Workshop>();
            }
            hero._ownedWorkshops.Remove(workshop);
            if (trackInOwnedList)
            {
                hero._ownedWorkshops.Add(workshop);
            }
            workshop._owner = hero;

            // The poisoned-save shape: the workshop is player-owned but the behavior storage has
            // no entry (and no capacity) for it.
            var workshopsBehavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
            workshopsBehavior._workshopData = Array.Empty<WorkshopsCampaignBehavior.WorkshopData>();
            workshopsBehavior._warehouseRosterPerSettlement = Array.Empty<KeyValuePair<Settlement, ItemRoster>>();

            // The capacity patch counts workshops attached to towns.
            var town = ObjectHelper.SkipConstructor<Town>();
            town.Workshops = new[] { workshop };
            Campaign.Current._towns.Add(town);
        });
    }

    private static void AssertWorkshopDataRestored(EnvironmentInstance instance, string workshopId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Workshop>(workshopId, out var workshop));

            var workshopsBehavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
            Assert.NotNull(workshopsBehavior.GetDataOfWorkshop(workshop));
            Assert.Single(workshopsBehavior._workshopData, data => data?.Workshop == workshop);
        });
    }
}
