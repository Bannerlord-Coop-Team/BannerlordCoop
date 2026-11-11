using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Smithing;

/// <summary>Verifies a client files a received town order under the town the server named, not where the owner stands.</summary>
public class CraftingCampaignBehaviorTownOrderHandlerTests : IDisposable
{
    private const string OrderTownId = "e2e_order_town";
    private const string OwnerTownId = "e2e_owner_town";
    private const string OrderOwnerId = "e2e_order_owner";
    private const string TemplateId = "e2e_order_template";
    private const string OrderId = "e2e_town_order";

    // These only build the pre-crafted item from real crafting data; the town an order is filed under does not depend on them.
    private static readonly MethodBase[] ItemGenerationMethods =
    {
        AccessTools.Method(typeof(CraftingCampaignBehavior), nameof(CraftingCampaignBehavior.GetWeaponPieces)),
        AccessTools.Constructor(typeof(WeaponDesign), new[] { typeof(CraftingTemplate), typeof(TextObject), typeof(WeaponDesignElement[]), typeof(string) }),
        AccessTools.Method(typeof(Crafting), nameof(Crafting.GenerateItem)),
    };

    private readonly E2ETestEnvironment environment;

    public CraftingCampaignBehaviorTownOrderHandlerTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void NetworkCreateTownOrder_OwnerInAnotherTown_FilesOrderUnderMessageTownOnEveryClient()
    {
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                var orderTown = client.CreateRegisteredObject<Town>(OrderTownId);
                var ownerTown = client.CreateRegisteredObject<Town>(OwnerTownId);
                var orderOwner = client.CreateRegisteredObject<Hero>(OrderOwnerId);
                client.CreateRegisteredObject<CraftingTemplate>(TemplateId);
                var order = client.CreateRegisteredObject<CraftingOrder>(OrderId);

                // v1.5.7 passes the town because the owner need not stand in it; this owner stands in another town.
                var ownerSettlement = ObjectHelper.SkipConstructor<Settlement>();
                using (new AllowedThread())
                {
                    ownerSettlement.Town = ownerTown;
                    orderOwner._stayingInSettlement = ownerSettlement;
                    order.OrderOwner = orderOwner;
                    order.PreCraftedWeaponDesignItem = ObjectHelper.SkipConstructor<ItemObject>();
                }

                var craftingBehavior = new CraftingCampaignBehavior();
                craftingBehavior._craftingOrders.Add(orderTown, new CraftingCampaignBehavior.CraftingOrderSlots());
                craftingBehavior._craftingOrders.Add(ownerTown, new CraftingCampaignBehavior.CraftingOrderSlots());
                Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { craftingBehavior }));
            });
        }

        // The message the server's SendTownOrderCreated sends after it files the order.
        var message = new NetworkCreateTownOrder(
            orderOwnerId: OrderOwnerId,
            craftingOrderId: OrderId,
            randomElementId: TemplateId,
            pieceTier: 1,
            nextTownOrderId: "town_order_0",
            townId: OrderTownId);

        environment.Server.Call(() => environment.Server.Resolve<INetwork>().SendAll(message), ItemGenerationMethods);

        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.Equal(OrderTownId, Assert.Single(client.InternalMessages.GetMessages<NetworkCreateTownOrder>()).TownId);

                var orderTown = client.GetRegisteredObject<Town>(OrderTownId);
                var ownerTown = client.GetRegisteredObject<Town>(OwnerTownId);
                var order = client.GetRegisteredObject<CraftingOrder>(OrderId);
                var craftingOrders = Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>()._craftingOrders;

                Assert.Same(order, craftingOrders[orderTown].Slots[order.DifficultyLevel]);
                Assert.All(craftingOrders[ownerTown].Slots, slot => Assert.Null(slot));
                Assert.Same(orderTown, Assert.Single(client.InternalMessages.GetMessages<RefreshWeaponDesignVM>()).Town);
            });
        }
    }
}
