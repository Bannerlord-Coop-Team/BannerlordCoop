using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using GameInterface.Services.Villages;
using GameInterface.Services.Villages.Interfaces;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Inventory;

/// <summary>
/// Covers a client closing an inventory screen while the server's reply to its previous close is still on the way.
/// Runs vanilla's screen logic and close path, and only delays the network between the two.
/// </summary>
public class InventoryEquipmentCloseTests : IDisposable
{
    private readonly string partyId;
    private readonly string armorId;
    private readonly string helmetId;
    private readonly string plateId;
    private readonly string mailId;
    private readonly string swordId;
    private readonly string daggerId;
    private GameStateManager? previousStates;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private TestNetworkRouter Router => Server.Resolve<TestNetworkRouter>();

    public InventoryEquipmentCloseTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        armorId = CreateEquippableItem("close_test_armor", ItemObject.ItemTypeEnum.BodyArmor);
        helmetId = CreateEquippableItem("close_test_helmet", ItemObject.ItemTypeEnum.HeadArmor);
        plateId = CreateEquippableItem("close_test_plate", ItemObject.ItemTypeEnum.BodyArmor);
        mailId = CreateEquippableItem("close_test_mail", ItemObject.ItemTypeEnum.BodyArmor);
        swordId = CreateEquippableItem("close_test_sword", ItemObject.ItemTypeEnum.OneHandedWeapon);
        daggerId = CreateEquippableItem("close_test_dagger", ItemObject.ItemTypeEnum.OneHandedWeapon);

        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var party = instance.GetRegisteredObject<MobileParty>(partyId);
                    if (party.MemberRoster.FindIndexOfTroop(party.LeaderHero.CharacterObject) < 0)
                        party.MemberRoster.AddToCounts(party.LeaderHero.CharacterObject, 1);

                    // Every machine agrees on a real hero's gear, but the harness seeds each copy with its own
                    Array.Clear(party.LeaderHero.BattleEquipment._itemSlots);
                }
            });
        }

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
            Server.Resolve<ISessionTradePlayerDataInterface>().AddPlayerKeys(heroId);

            party.ItemRoster.Clear();
            party.ItemRoster.AddToCounts(Server.GetRegisteredObject<ItemObject>(armorId), 1);
            foreach (var itemId in new[] { helmetId, plateId, mailId, swordId, daggerId })
                party.ItemRoster.AddToCounts(Server.GetRegisteredObject<ItemObject>(itemId), 1);
        });
        TestEnvironment.FlushCoalescer();

        Client.Call(() =>
        {
            Assert.Equal(1, RosterCount(Client, armorId));

            // The inventory always opens on top of the map, and popping the last state would end the game
            var states = Game.Current.GameStateManager;
            states._gameStates.Add(states.CreateState<MapState>());

            // Vanilla's close and the received-update handlers read the global state manager, which the harness leaves unset
            previousStates = GameStateManager._current;
            GameStateManager._current = states;
        });
    }

    public void Dispose()
    {
        GameStateManager._current = previousStates;
        TestEnvironment.Dispose();
    }

    [Fact]
    public void EquipWithNoOtherScreen_ReachesTheServerAndComesBack()
    {
        PauseNetwork();
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        // The screen's change is sent, then undone locally until the server answers
        Assert.Single(Client.NetworkSentMessages.GetMessages<CompleteTrade>());
        Client.Call(() => Assert.Null(ItemIdOf(Client, Client.GetRegisteredObject<MobileParty>(partyId).LeaderHero.BattleEquipment[EquipmentIndex.Body])));
        Assert.Equal(1, RosterCountOnClient(armorId));

        ResumeNetwork();

        AssertEquippedEverywhere(armorId, EquipmentIndex.Body);
    }

    [Fact]
    public void CancellingAScreenOpenedBeforeTheReply_KeepsTheEquippedItem()
    {
        PauseNetwork();
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        OpenScreen();
        ResumeNetwork();
        PauseNetwork();

        CloseScreen(fromCancel: true);
        ResumeNetwork();

        AssertEquippedEverywhere(armorId, EquipmentIndex.Body);
    }

    [Fact]
    public void ClosingAScreenBeforeTheFirstCloseReachedTheServer_KeepsTheEquippedItem()
    {
        PauseNetwork();
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        OpenScreen();
        CloseScreen(fromCancel: true);
        ResumeNetwork();

        AssertEquippedEverywhere(armorId, EquipmentIndex.Body);
    }

    [Fact]
    public void ReplyToAnEarlierCloseLandingOnAnOpenScreen_KeepsThePendingEquip()
    {
        PauseNetwork();
        OpenScreen();
        CloseScreen(fromCancel: true);

        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        ResumeNetwork();
        PauseNetwork();

        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEquippedEverywhere(armorId, EquipmentIndex.Body);
    }

    [Fact]
    public void SecondEquipBeforeTheFirstReply_KeepsBothItems()
    {
        PauseNetwork();
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        OpenScreen();
        Equip(helmetId, EquipmentIndex.Head);
        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEquippedEverywhere(armorId, EquipmentIndex.Body);
        AssertEquippedEverywhere(helmetId, EquipmentIndex.Head);
    }

    [Fact]
    public void SameSlotEquip_ReplyLandsOnTheOpenScreen_RefusesTheLaterClose()
    {
        PauseNetwork();
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        OpenScreen();
        Equip(plateId, EquipmentIndex.Body);
        ResumeNetwork();
        PauseNetwork();

        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEverywhere(EquipmentIndex.Body, armorId, (armorId, 0), (plateId, 1));
    }

    [Fact]
    public void SameSlotEquip_BothClosesBeforeAnyReply_RefusesTheLaterClose()
    {
        PauseNetwork();
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        OpenScreen();
        Equip(plateId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEverywhere(EquipmentIndex.Body, armorId, (armorId, 0), (plateId, 1));
    }

    [Fact]
    public void CancellingASameSlotEquip_KeepsTheAcceptedItemAndReturnsThePendingOne()
    {
        PauseNetwork();
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        OpenScreen();
        Equip(plateId, EquipmentIndex.Body);
        ResumeNetwork();
        PauseNetwork();

        CloseScreen(fromCancel: true);
        ResumeNetwork();

        AssertEverywhere(EquipmentIndex.Body, armorId, (armorId, 0), (plateId, 1));
    }

    [Fact]
    public void ReplacingAnEquippedItemTwiceBeforeTheReply_RefusesTheLaterClose()
    {
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);
        Router.RunUntilIdle();
        TestEnvironment.FlushCoalescer();
        AssertEquippedEverywhere(armorId, EquipmentIndex.Body);

        PauseNetwork();
        OpenScreen();
        Equip(plateId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        OpenScreen();
        Equip(mailId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEverywhere(EquipmentIndex.Body, plateId, (armorId, 1), (plateId, 0), (mailId, 1));
    }

    [Fact]
    public void EquippingTheOnlyCopyInTwoSlotsBeforeTheReply_DoesNotDuplicateIt()
    {
        PauseNetwork();
        OpenScreen();
        Equip(swordId, EquipmentIndex.Weapon0);
        CloseScreen(fromCancel: false);

        OpenScreen();
        Equip(swordId, EquipmentIndex.Weapon1);
        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEverywhere(EquipmentIndex.Weapon0, swordId, (swordId, 0));
        AssertEverywhere(EquipmentIndex.Weapon1, null, (swordId, 0));
    }

    [Fact]
    public void EquippingTheOnlyCopyInTheSameSlotTwiceBeforeTheReply_DoesNotDuplicateIt()
    {
        PauseNetwork();
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEquippedEverywhere(armorId, EquipmentIndex.Body);
    }

    [Fact]
    public void ServerSlotWriteUnderAPendingEquip_RefusesTheCloseAndShowsTheServersSlot()
    {
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        ServerWritesSlot(plateId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);
        Router.RunUntilIdle();
        TestEnvironment.FlushCoalescer();

        AssertEverywhere(EquipmentIndex.Body, plateId, (armorId, 1), (plateId, 1));
    }

    [Fact]
    public void LinkedSlotMovesAfterTheRosterLostAnItem_RefusesTheWholeClose()
    {
        OpenScreen();
        Equip(swordId, EquipmentIndex.Weapon1);
        CloseScreen(fromCancel: false);
        Router.RunUntilIdle();
        TestEnvironment.FlushCoalescer();

        PauseNetwork();
        OpenScreen();
        Equip(daggerId, EquipmentIndex.Weapon2);
        CloseScreen(fromCancel: false);

        // Weapon0 only stays affordable if Weapon1 really gives the sword up, and Weapon1 needs the dagger that just left
        OpenScreen();
        MoveBetweenSlots(swordId, EquipmentIndex.Weapon1, EquipmentIndex.Weapon0);
        Equip(daggerId, EquipmentIndex.Weapon1);
        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEverywhere(EquipmentIndex.Weapon0, null, (swordId, 0), (daggerId, 0));
        AssertEverywhere(EquipmentIndex.Weapon1, swordId, (swordId, 0), (daggerId, 0));
        AssertEverywhere(EquipmentIndex.Weapon2, daggerId, (swordId, 0), (daggerId, 0));
    }

    [Fact]
    public void RejectedForceTransferClose_AfterAReplyLandedOnIt_LeavesBothMachinesOnTheServersSlot()
    {
        var settlementId = MovePlateToSettlementStock();
        string? poolId = null;
        Server.Call(() =>
        {
            var service = Server.Resolve<IVillageHostileActionInterface>();
            poolId = service.AuthorizeForceTransfer(default, partyId, settlementId, Array.Empty<ItemRosterElementData>(), null, 0).RequestId;
            Assert.True(service.TryConsumeForceTransfer(poolId, partyId, out _));
        });

        PauseNetwork();
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        OpenSettlementScreen(settlementId, isTrading: false);
        Client.Call(() => ForceTransferScreenTracker.NoteLootScreenOpened(poolId!, Client.GetRegisteredObject<Settlement>(settlementId).ItemRoster));
        EquipFromSettlement(plateId, EquipmentIndex.Body);
        ResumeNetwork();
        PauseNetwork();

        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEverywhere(EquipmentIndex.Body, armorId, (armorId, 0), (plateId, 0));
    }

    [Fact]
    public void EquippedPurchaseTheMerchantCanNoLongerSupply_EmptiesTheSlotAndRefunds()
    {
        var settlementId = MovePlateToSettlementStock();
        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
            instance.Call(() => instance.GetRegisteredObject<ItemObject>(plateId).Value = 100);
        Server.Call(() => Server.GetRegisteredObject<MobileParty>(partyId).LeaderHero.Gold = 1000);
        TestEnvironment.FlushCoalescer();

        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);
        Router.RunUntilIdle();
        TestEnvironment.FlushCoalescer();

        PauseNetwork();
        OpenSettlementScreen(settlementId, isTrading: true);
        EquipFromSettlement(plateId, EquipmentIndex.Body);
        Server.Call(() => Server.GetRegisteredObject<Settlement>(settlementId).ItemRoster.AddToCounts(Server.GetRegisteredObject<ItemObject>(plateId), -1));

        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEverywhere(EquipmentIndex.Body, null, (armorId, 1), (plateId, 0));
        Server.Call(() => Assert.Equal(1000, Server.GetRegisteredObject<MobileParty>(partyId).LeaderHero.Gold));
    }

    [Fact]
    public void SellingAnItemAnEarlierCloseEquipped_RefusesTheCloseWithoutPayingForIt()
    {
        var settlementId = MovePlateToSettlementStock();
        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
            instance.Call(() => instance.GetRegisteredObject<ItemObject>(armorId).Value = 100);
        Server.Call(() => Server.GetRegisteredObject<MobileParty>(partyId).LeaderHero.Gold = 1000);
        TestEnvironment.FlushCoalescer();

        PauseNetwork();
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        OpenSettlementScreen(settlementId, isTrading: true);
        Client.Call(() =>
        {
            var state = (InventoryState)Game.Current.GameStateManager.ActiveState;
            var element = new ItemRosterElement(Client.GetRegisteredObject<ItemObject>(armorId), 1);
            state.InventoryLogic.AddTransferCommand(TransferCommand.Transfer(1, InventoryLogic.InventorySide.PlayerInventory,
                InventoryLogic.InventorySide.OtherInventory, element, EquipmentIndex.None, EquipmentIndex.None, null));
        });
        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEquippedEverywhere(armorId, EquipmentIndex.Body);
        Server.Call(() =>
        {
            var armor = Server.GetRegisteredObject<ItemObject>(armorId);
            Assert.Equal(0, Server.GetRegisteredObject<Settlement>(settlementId).ItemRoster.GetItemNumber(armor));
            Assert.Equal(1000, Server.GetRegisteredObject<MobileParty>(partyId).LeaderHero.Gold);
        });
    }

    // Registers an item in every instance's native catalog, because equipment slots cross the wire by item id
    private string CreateEquippableItem(string stringId, ItemObject.ItemTypeEnum type)
    {
        var id = TestEnvironment.CreateRegisteredObject<ItemObject>();
        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var item = instance.GetRegisteredObject<ItemObject>(id);
                item.StringId = stringId;
                item.ItemType = type;
                item.Name = new TextObject(stringId);
                MBObjectManager.Instance.RegisterObject(item);
            });
        }

        return id;
    }

    // Holds traffic in both directions, as a slow server or connection would
    private void PauseNetwork()
    {
        Router.PauseLink(Client.NetPeer, Server.NetPeer);
        Router.PauseLink(Server.NetPeer, Client.NetPeer);
    }

    // Delivers what the client sent, then what the server answered with
    private void ResumeNetwork()
    {
        Router.ResumeLink(Client.NetPeer, Server.NetPeer);
        Router.RunUntilIdle();
        TestEnvironment.FlushCoalescer();
        Router.ResumeLink(Server.NetPeer, Client.NetPeer);
        Router.RunUntilIdle();
    }

    // Opens the screen the way vanilla's party inventory does, at base prices because the harness has no market
    private void OpenScreen()
    {
        Client.Call(() =>
        {
            var party = Client.GetRegisteredObject<MobileParty>(partyId);
            var character = party.LeaderHero.CharacterObject;
            var states = Game.Current.GameStateManager;

            var logic = new InventoryLogic(party, character, null);
            var state = states.CreateState<InventoryState>();
            logic.Initialize(new ItemRoster(), party.ItemRoster, party.MemberRoster, isTrading: false, isSpecialActionsPermitted: true,
                character, InventoryScreenHelper.InventoryCategoryType.None, null, useBasePrices: true, state.InventoryMode);
            state.InventoryLogic = logic;
            states._gameStates.Add(state);
        });
    }

    // Moves an item from the party inventory to a battle slot through vanilla's transfer command
    private void Equip(string itemId, EquipmentIndex slot)
    {
        Client.Call(() =>
        {
            var state = (InventoryState)Game.Current.GameStateManager.ActiveState;
            var character = Client.GetRegisteredObject<MobileParty>(partyId).LeaderHero.CharacterObject;
            var element = new ItemRosterElement(Client.GetRegisteredObject<ItemObject>(itemId), 1);

            state.InventoryLogic.AddTransferCommand(TransferCommand.Transfer(1, InventoryLogic.InventorySide.PlayerInventory,
                InventoryLogic.InventorySide.BattleEquipment, element, EquipmentIndex.None, slot, character));

            Assert.Equal(itemId, ItemIdOf(Client, character.FirstBattleEquipment[slot]));
        });
    }

    // Moves an item between two equipment slots through vanilla's transfer command
    private void MoveBetweenSlots(string itemId, EquipmentIndex from, EquipmentIndex to)
    {
        Client.Call(() =>
        {
            var state = (InventoryState)Game.Current.GameStateManager.ActiveState;
            var character = Client.GetRegisteredObject<MobileParty>(partyId).LeaderHero.CharacterObject;
            var element = new ItemRosterElement(Client.GetRegisteredObject<ItemObject>(itemId), 1);

            state.InventoryLogic.AddTransferCommand(TransferCommand.Transfer(1, InventoryLogic.InventorySide.BattleEquipment,
                InventoryLogic.InventorySide.BattleEquipment, element, from, to, character));
        });
    }

    // Takes the plate out of the party inventory and makes it the only stock of a new settlement
    private string MovePlateToSettlementStock()
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        Server.Call(() =>
        {
            var plate = Server.GetRegisteredObject<ItemObject>(plateId);
            Server.GetRegisteredObject<MobileParty>(partyId).ItemRoster.AddToCounts(plate, -1);

            var stock = Server.GetRegisteredObject<Settlement>(settlementId).ItemRoster;
            stock.Clear();
            stock.AddToCounts(plate, 1);
        });
        TestEnvironment.FlushCoalescer();
        return settlementId;
    }

    // Opens a loot or trade screen with the settlement's stock on the left, at base prices because the harness has no market
    private void OpenSettlementScreen(string settlementId, bool isTrading)
    {
        Client.Call(() =>
        {
            var party = Client.GetRegisteredObject<MobileParty>(partyId);
            var character = party.LeaderHero.CharacterObject;
            var states = Game.Current.GameStateManager;
            var mode = isTrading ? InventoryScreenHelper.InventoryMode.Trade : InventoryScreenHelper.InventoryMode.Loot;

            var logic = new InventoryLogic(party, character, null);
            logic.Initialize(Client.GetRegisteredObject<Settlement>(settlementId).ItemRoster, party.ItemRoster, party.MemberRoster, isTrading,
                isSpecialActionsPermitted: true, character, InventoryScreenHelper.InventoryCategoryType.None, null, useBasePrices: true, mode);

            // Vanilla's view model hooks this, and the harness has none
            logic.TotalAmountChange = _ => { };

            var state = states.CreateState<InventoryState>();
            state.InventoryLogic = logic;
            states._gameStates.Add(state);
        });
    }

    // Moves an item from the settlement's stock straight onto a battle slot through vanilla's transfer command
    private void EquipFromSettlement(string itemId, EquipmentIndex slot)
    {
        Client.Call(() =>
        {
            var state = (InventoryState)Game.Current.GameStateManager.ActiveState;
            var character = Client.GetRegisteredObject<MobileParty>(partyId).LeaderHero.CharacterObject;
            var element = new ItemRosterElement(Client.GetRegisteredObject<ItemObject>(itemId), 1);

            state.InventoryLogic.AddTransferCommand(TransferCommand.Transfer(1, InventoryLogic.InventorySide.OtherInventory,
                InventoryLogic.InventorySide.BattleEquipment, element, EquipmentIndex.None, slot, character));
        });
    }

    // Presses Done or Cancel through vanilla's close path
    private void CloseScreen(bool fromCancel)
    {
        Client.Call(() => InventoryScreenHelper.CloseScreen(fromCancel));
    }

    // Writes a slot through vanilla's equipment setter on the server, the way campaign code changes a hero's gear
    private void ServerWritesSlot(string itemId, EquipmentIndex slot)
    {
        Server.Call(() =>
        {
            var hero = Server.GetRegisteredObject<MobileParty>(partyId).LeaderHero;
            hero.BattleEquipment[slot] = new EquipmentElement(Server.GetRegisteredObject<ItemObject>(itemId));
        });
    }

    // Asserts the item is in the slot and out of the party inventory on both machines
    private void AssertEquippedEverywhere(string itemId, EquipmentIndex slot) => AssertEverywhere(slot, itemId, (itemId, 0));

    // Compares both machines at once, so a failure shows where each item ended up on each
    private void AssertEverywhere(EquipmentIndex slot, string? slotItemId, params (string itemId, int count)[] roster)
    {
        var rosterText = string.Join(" ", roster.Select(item => $"{item.itemId}={item.count}"));
        var expected = $"server: slot={slotItemId} {rosterText}, client: slot={slotItemId} {rosterText}";
        var actual = $"server: {Describe(Server, slot, roster)}, client: {Describe(Client, slot, roster)}";
        Assert.True(expected == actual, $"Expected {expected}, actual {actual}");
    }

    // Reports what one machine has in the slot and in the party inventory
    private string Describe(EnvironmentInstance instance, EquipmentIndex slot, (string itemId, int count)[] roster)
    {
        string description = string.Empty;
        instance.Call(() =>
        {
            var hero = instance.GetRegisteredObject<MobileParty>(partyId).LeaderHero;
            var counts = roster.Select(item => $"{item.itemId}={RosterCount(instance, item.itemId)}");
            description = $"slot={ItemIdOf(instance, hero.BattleEquipment[slot])} {string.Join(" ", counts)}";
        });
        return description;
    }

    // Reads the client's party inventory count from inside its scope
    private int RosterCountOnClient(string itemId)
    {
        int count = -1;
        Client.Call(() => count = RosterCount(Client, itemId));
        return count;
    }

    // Counts the item in the party inventory, and must run inside the instance's scope
    private int RosterCount(EnvironmentInstance instance, string itemId) =>
        instance.GetRegisteredObject<MobileParty>(partyId).ItemRoster.GetItemNumber(instance.GetRegisteredObject<ItemObject>(itemId));

    // Gets the registry id of the item in a slot, or null for an empty slot
    private static string? ItemIdOf(EnvironmentInstance instance, EquipmentElement element)
    {
        if (element.Item == null) return null;
        return instance.ObjectManager.TryGetId(element.Item, out var id) ? id : element.Item.StringId;
    }
}
