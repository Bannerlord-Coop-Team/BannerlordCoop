using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Actions.Patches;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Workshops.Commands;
using GameInterface.Services.Workshops.Interfaces;
using HarmonyLib;
using WarehouseOwnerChanged = GameInterface.Services.Workshops.Messages.ChangeWorkshopOwner;
using WarehouseOwnerChangedEvent = GameInterface.Services.Workshops.Messages.WorkshopOwnerChanged;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Workshops;

public class WorkshopPurchaseConversationTests : IDisposable
{
    private const string BuyerControllerId = "WorkshopBuyer";
    private const int WorkshopCapital = 1000;
    private const int WorkshopCost = 400;
    private static Workshop expectedDispatchedWorkshop;
    private static Hero expectedDispatchedOldOwner;
    private static int observedOwnerChangeDispatches;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public WorkshopPurchaseConversationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ClientPurchaseWorkshop_ServerAppliesOwnerGoldAndRefreshesWorkshopList()
    {
        var client = Clients.First();
        var state = CreatePurchaseState(workshopOwnedByBuyer: false, buyerGold: 1000, sellerGold: 0);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Workshop>(state.WorkshopId, out var workshop));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(state.BuyerId, out var buyer));
            Assert.True(client.ObjectManager.TryGetObject<WorkshopType>(state.WorkshopTypeId, out var workshopType));

            Assert.False(ChangeOwnerOfWorkshopActionPatches.ApplyInternalPrefix(workshop, buyer, workshopType, WorkshopCapital, WorkshopCost));
        });

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<ChangeWorkshopOwner>());
        Assert.Equal(state.WorkshopId, request.WorkshopId);
        Assert.Equal(state.SellerId, request.ExpectedOwnerId);
        Assert.Equal(state.BuyerId, request.NewOwnerId);
        Assert.Equal(state.WorkshopTypeId, request.WorkshopTypeId);
        Assert.Equal(WorkshopCapital, request.Capital);
        Assert.Equal(WorkshopCost, request.Cost);

        AssertWorkshopOwnedByBuyer(Server, state, expectedBuyerGold: 600, expectedSellerGold: 400);
        foreach (var environmentClient in Clients)
        {
            AssertWorkshopOwnedByBuyer(environmentClient, state, expectedBuyerGold: 600, expectedSellerGold: 400);
        }

        AssertClanWorkshopDataReadyForClanMenu(client, state);
        Assert.Single(Server.NetworkSentMessages.GetMessages<RefreshWorkshopsList>());
    }

    [Fact]
    public void DebugOwnerChange_ServerPreparesClientWarehouseDataForClanMenu()
    {
        var client = Clients.First();
        var state = CreatePurchaseState(workshopOwnedByBuyer: false, buyerGold: 1000, sellerGold: 0);
        var harmony = new Harmony("e2e.workshops.debug-owner-change");
        Workshop serverWorkshop = null;
        Hero serverOldOwner = null;
        int originalCapital = 0;

        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Workshop>(state.WorkshopId, out serverWorkshop));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(state.BuyerId, out var buyer));

                serverOldOwner = serverWorkshop.Owner;
                originalCapital = serverWorkshop.Capital;
                expectedDispatchedWorkshop = serverWorkshop;
                expectedDispatchedOldOwner = serverOldOwner;
                observedOwnerChangeDispatches = 0;
                Server.Resolve<ISessionWorkshopPlayerDataInterface>().AddPlayerKeys(state.BuyerId);

                harmony.Patch(
                    AccessTools.Method(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWorkshopOwnerChanged)),
                    prefix: new HarmonyMethod(typeof(WorkshopPurchaseConversationTests), nameof(ObserveOwnerChangeDispatch)));

                WorkshopDebugCommand.ApplyOwnerChange(serverWorkshop, buyer);
            });

            Assert.Equal(1, observedOwnerChangeDispatches);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            expectedDispatchedWorkshop = null;
            expectedDispatchedOldOwner = null;
        }

        Server.Call(() =>
        {
            // The E2E harness does not register campaign behavior events. Publish the patched
            // behavior event separately after proving the action reached CampaignEventDispatcher.
            var workshopsBehavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
            MessageBroker.Instance.Publish(
                workshopsBehavior,
                new WarehouseOwnerChangedEvent(serverWorkshop, serverOldOwner));
        });

        AssertWorkshopOwnedByBuyer(Server, state, expectedBuyerGold: 1000, expectedSellerGold: 0);
        Assert.Single(Server.NetworkSentMessages.GetMessages<WarehouseOwnerChanged>());
        foreach (var environmentClient in Clients)
        {
            AssertWorkshopOwnedByBuyer(environmentClient, state, expectedBuyerGold: 1000, expectedSellerGold: 0);
        }
        AssertWorkshopCapital(Server, state, originalCapital);
        foreach (var environmentClient in Clients)
        {
            AssertWorkshopCapital(environmentClient, state, originalCapital);
        }

        AssertClanWorkshopDataReadyForClanMenu(client, state);
    }

    private static void ObserveOwnerChangeDispatch(Workshop workshop, Hero oldOwner)
    {
        if (workshop == expectedDispatchedWorkshop && oldOwner == expectedDispatchedOldOwner)
        {
            observedOwnerChangeDispatches++;
        }
    }

    [Fact]
    public void DuplicatePurchaseRequest_WhenWorkshopAlreadyOwnedByBuyer_RefreshesWithoutChargingAgain()
    {
        var client = Clients.First();
        var state = CreatePurchaseState(workshopOwnedByBuyer: true, buyerGold: 600, sellerGold: 400);

        client.Call(() => client.Resolve<Common.Network.INetwork>().SendAll(new ChangeWorkshopOwner(
            state.WorkshopId,
            state.BuyerId,
            state.BuyerId,
            state.WorkshopTypeId,
            WorkshopCapital,
            WorkshopCost)));

        AssertWorkshopOwnedByBuyer(Server, state, expectedBuyerGold: 600, expectedSellerGold: 400);
        foreach (var environmentClient in Clients)
        {
            AssertWorkshopOwnedByBuyer(environmentClient, state, expectedBuyerGold: 600, expectedSellerGold: 400);
        }

        Assert.Single(Server.NetworkSentMessages.GetMessages<RefreshWorkshopsList>());
    }

    [Fact]
    public void SimultaneousPurchaseRequest_WhenOwnerChangedBeforeSecondApply_RejectsStaleBuyerWithoutCharging()
    {
        var clients = Clients.ToArray();
        var firstClient = clients[0];
        var secondClient = clients[1];
        var state = CreatePurchaseState(workshopOwnedByBuyer: false, buyerGold: 1000, sellerGold: 0);
        var secondBuyerId = TestEnvironment.CreateRegisteredObject<Hero>();

        RegisterPlayerHero(Server, secondBuyerId, "WorkshopBuyerTwo");
        foreach (var client in Clients)
        {
            RegisterPlayerHero(client, secondBuyerId, "WorkshopBuyerTwo");
        }
        PrepareBuyer(secondBuyerId, 1000);
        ClearMessages(Server);
        foreach (var client in Clients)
        {
            ClearMessages(client);
        }

        firstClient.Call(() => firstClient.Resolve<Common.Network.INetwork>().SendAll(new ChangeWorkshopOwner(
            state.WorkshopId,
            state.SellerId,
            state.BuyerId,
            state.WorkshopTypeId,
            WorkshopCapital,
            WorkshopCost)));

        secondClient.Call(() => secondClient.Resolve<Common.Network.INetwork>().SendAll(new ChangeWorkshopOwner(
            state.WorkshopId,
            state.SellerId,
            secondBuyerId,
            state.WorkshopTypeId,
            WorkshopCapital,
            WorkshopCost)));

        AssertWorkshopOwnedByBuyer(Server, state, expectedBuyerGold: 600, expectedSellerGold: 400);
        AssertHeroGold(Server, secondBuyerId, 1000);
        foreach (var environmentClient in Clients)
        {
            AssertWorkshopOwnedByBuyer(environmentClient, state, expectedBuyerGold: 600, expectedSellerGold: 400);
            AssertHeroGold(environmentClient, secondBuyerId, 1000);
        }

        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<RefreshWorkshopsList>().Count());
    }

    [Fact]
    public void ClientPrediction_WhenBuyingWorkshop_UpdatesLocalOwnerListsWithoutChangingGold()
    {
        var client = Clients.First();
        var state = CreatePurchaseState(workshopOwnedByBuyer: false, buyerGold: 1000, sellerGold: 0);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Workshop>(state.WorkshopId, out var workshop));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(state.BuyerId, out var buyer));
            ChangeOwnerOfWorkshopActionPatches.ApplyPredictedWorkshopOwnership(workshop, buyer);
            ChangeOwnerOfWorkshopActionPatches.ApplyPredictedWorkshopData(workshop, buyer);
        });

        Assert.Empty(client.NetworkSentMessages.GetMessages<ChangeWorkshopOwner>());
        AssertWorkshopOwnedByBuyer(client, state, expectedBuyerGold: 1000, expectedSellerGold: 0);
        AssertClanWorkshopDataReadyForClanMenu(client, state);
    }

    [Fact]
    public void ClientPrediction_WhenBehaviorStorageHasNoFreeSlots_PreparesClanFinanceWorkshopData()
    {
        var client = Clients.First();
        var state = CreatePurchaseState(workshopOwnedByBuyer: false, buyerGold: 1000, sellerGold: 0);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Workshop>(state.WorkshopId, out var workshop));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(state.BuyerId, out var buyer));

            var workshopsBehavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
            workshopsBehavior._workshopData = Array.Empty<WorkshopsCampaignBehavior.WorkshopData>();
            workshopsBehavior._warehouseRosterPerSettlement = Array.Empty<KeyValuePair<Settlement, ItemRoster>>();

            ChangeOwnerOfWorkshopActionPatches.ApplyPredictedWorkshopOwnership(workshop, buyer);
            ChangeOwnerOfWorkshopActionPatches.ApplyPredictedWorkshopData(workshop, buyer);
        });

        AssertWorkshopOwnedByBuyer(client, state, expectedBuyerGold: 1000, expectedSellerGold: 0);
        AssertClanWorkshopDataReadyForClanMenu(client, state);
    }

    [Fact]
    public void ClientPrediction_WhenOwnedWorkshopAddSyncArrives_DoesNotDuplicateWorkshop()
    {
        var client = Clients.First();
        var state = CreatePurchaseState(workshopOwnedByBuyer: false, buyerGold: 1000, sellerGold: 0);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Workshop>(state.WorkshopId, out var workshop));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(state.BuyerId, out var buyer));
            ChangeOwnerOfWorkshopActionPatches.ApplyPredictedWorkshopOwnership(workshop, buyer);
        });

        client.SimulateMessage(this, new AddOwnedWorkshop(state.BuyerId, state.WorkshopId));

        AssertWorkshopOwnedByBuyer(client, state, expectedBuyerGold: 1000, expectedSellerGold: 0);
    }

    [Fact]
    public void ClientStalePurchaseOption_WhenWorkshopAlreadyOwnedByBuyer_DoesNotSendDuplicatePurchaseRequest()
    {
        var client = Clients.First();
        var state = CreatePurchaseState(workshopOwnedByBuyer: true, buyerGold: 600, sellerGold: 400);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Workshop>(state.WorkshopId, out var workshop));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(state.BuyerId, out var buyer));
            Assert.True(client.ObjectManager.TryGetObject<WorkshopType>(state.WorkshopTypeId, out var workshopType));

            Assert.False(ChangeOwnerOfWorkshopActionPatches.ApplyInternalPrefix(workshop, buyer, workshopType, WorkshopCapital, WorkshopCost));
        });

        Assert.Empty(client.NetworkSentMessages.GetMessages<ChangeWorkshopOwner>());
        AssertWorkshopOwnedByBuyer(client, state, expectedBuyerGold: 600, expectedSellerGold: 400);
        AssertClanWorkshopDataReadyForClanMenu(client, state);
    }

    private PurchaseState CreatePurchaseState(bool workshopOwnedByBuyer, int buyerGold, int sellerGold)
    {
        var workshopId = TestEnvironment.CreateRegisteredObject<Workshop>();
        var buyerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var sellerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var workshopTypeId = TestEnvironment.CreateRegisteredObject<WorkshopType>();
        var state = new PurchaseState(workshopId, buyerId, sellerId, workshopTypeId);

        RegisterPlayerHero(Server, buyerId);
        foreach (var client in Clients)
        {
            RegisterPlayerHero(client, buyerId);
        }

        PrepareInstance(Server, state, workshopOwnedByBuyer, buyerGold, sellerGold);
        foreach (var client in Clients)
        {
            PrepareInstance(client, state, workshopOwnedByBuyer, buyerGold, sellerGold);
        }

        ClearMessages(Server);
        foreach (var client in Clients)
        {
            ClearMessages(client);
        }

        return state;
    }

    private static void RegisterPlayerHero(EnvironmentInstance instance, string buyerId)
    {
        RegisterPlayerHero(instance, buyerId, BuyerControllerId);
    }

    private static void RegisterPlayerHero(EnvironmentInstance instance, string buyerId, string controllerId)
    {
        instance.Call(() =>
        {
            instance.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
            Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(new Player(
                controllerId,
                buyerId,
                string.Empty,
                string.Empty,
                string.Empty)));

            Assert.True(instance.ObjectManager.TryGetObject<Hero>(buyerId, out var buyer));
            using (new AllowedThread())
            {
                Game.Current.PlayerTroop = buyer.CharacterObject;
                Campaign.Current.PlayerDefaultFaction = buyer.Clan;
            }
        });
    }

    private static void PrepareInstance(EnvironmentInstance instance, PurchaseState state, bool workshopOwnedByBuyer, int buyerGold, int sellerGold)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Workshop>(state.WorkshopId, out var workshop));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(state.BuyerId, out var buyer));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(state.SellerId, out var seller));

            buyer.Gold = buyerGold;
            seller.Gold = sellerGold;
            EnsureWorkshopBehaviorStorage();
            EnsureOwnedWorkshops(buyer);
            EnsureOwnedWorkshops(seller);
            buyer._ownedWorkshops.Remove(workshop);
            seller._ownedWorkshops.Remove(workshop);

            if (workshopOwnedByBuyer)
            {
                workshop._owner = buyer;
                buyer._ownedWorkshops.Add(workshop);
            }
            else
            {
                workshop._owner = seller;
                seller._ownedWorkshops.Add(workshop);
            }
        });
    }

    private void PrepareBuyer(string buyerId, int buyerGold)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(buyerId, out var buyer));
                buyer.Gold = buyerGold;
                EnsureOwnedWorkshops(buyer);
            });
        }
    }

    private void AssertHeroGold(EnvironmentInstance instance, string heroId, int expectedGold)
    {
        // Hero.Gold is coalesced; drain the buffer before reading it on a client.
        TestEnvironment.FlushCoalescer();
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.Equal(expectedGold, hero.Gold);
        });
    }

    private void AssertWorkshopOwnedByBuyer(EnvironmentInstance instance, PurchaseState state, int expectedBuyerGold, int expectedSellerGold)
    {
        // buyer/seller Gold is coalesced; drain the buffer before reading it on a client.
        TestEnvironment.FlushCoalescer();
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Workshop>(state.WorkshopId, out var workshop));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(state.BuyerId, out var buyer));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(state.SellerId, out var seller));

            Assert.Equal(buyer, workshop.Owner);
            Assert.Contains(workshop, buyer.OwnedWorkshops);
            Assert.Single(buyer.OwnedWorkshops, w => w == workshop);
            Assert.DoesNotContain(workshop, seller.OwnedWorkshops);
            Assert.Equal(expectedBuyerGold, buyer.Gold);
            Assert.Equal(expectedSellerGold, seller.Gold);
        });
    }

    private static void AssertClanWorkshopDataReadyForClanMenu(EnvironmentInstance instance, PurchaseState state)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Workshop>(state.WorkshopId, out var workshop));

            var workshopsBehavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
            Assert.NotNull(workshopsBehavior.GetDataOfWorkshop(workshop));
            Assert.NotNull(workshopsBehavior.GetWarehouseRoster(workshop.Settlement));
            workshopsBehavior.GetWarehouseItemRosterWeight(workshop.Settlement);
            Assert.Single(workshopsBehavior._workshopData, data => data?.Workshop == workshop);
        });
    }

    private static void AssertWorkshopCapital(
        EnvironmentInstance instance,
        PurchaseState state,
        int expectedCapital)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Workshop>(state.WorkshopId, out var workshop));
            Assert.Equal(expectedCapital, workshop.Capital);
        });
    }

    private static void EnsureWorkshopBehaviorStorage()
    {
        var workshopsBehavior = Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
        const int minWorkshopDataCount = 8;

        if (workshopsBehavior._workshopData == null || workshopsBehavior._workshopData.Length == 0)
        {
            workshopsBehavior._workshopData = new WorkshopsCampaignBehavior.WorkshopData[minWorkshopDataCount];
        }
        if (workshopsBehavior._warehouseRosterPerSettlement == null || workshopsBehavior._warehouseRosterPerSettlement.Length == 0)
        {
            workshopsBehavior._warehouseRosterPerSettlement = new KeyValuePair<Settlement, ItemRoster>[minWorkshopDataCount];
        }
    }

    private static void EnsureOwnedWorkshops(Hero hero)
    {
        if (hero._ownedWorkshops == null)
        {
            hero._ownedWorkshops = new MBList<Workshop>();
        }
    }

    private static void ClearMessages(EnvironmentInstance instance)
    {
        instance.InternalMessages.Clear();
        instance.NetworkSentMessages.Clear();
    }

    private readonly struct PurchaseState
    {
        public readonly string WorkshopId;
        public readonly string BuyerId;
        public readonly string SellerId;
        public readonly string WorkshopTypeId;

        public PurchaseState(string workshopId, string buyerId, string sellerId, string workshopTypeId)
        {
            WorkshopId = workshopId;
            BuyerId = buyerId;
            SellerId = sellerId;
            WorkshopTypeId = workshopTypeId;
        }
    }
}
