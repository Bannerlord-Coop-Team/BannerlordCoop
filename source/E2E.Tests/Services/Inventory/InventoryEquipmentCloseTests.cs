using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
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
            foreach (var itemId in new[] { helmetId, plateId, mailId, swordId })
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
    public void SameSlotEquip_ReplyLandsOnTheOpenScreen_ReturnsTheFirstItemToTheRoster()
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

        AssertEverywhere(EquipmentIndex.Body, plateId, (armorId, 1), (plateId, 0));
    }

    [Fact]
    public void SameSlotEquip_BothClosesBeforeAnyReply_ReturnsTheFirstItemToTheRoster()
    {
        PauseNetwork();
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);

        OpenScreen();
        Equip(plateId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);
        ResumeNetwork();

        AssertEverywhere(EquipmentIndex.Body, plateId, (armorId, 1), (plateId, 0));
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
    public void ReplacingAnEquippedItemTwiceBeforeTheReply_ReturnsBothReplacedItems()
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

        AssertEverywhere(EquipmentIndex.Body, mailId, (armorId, 1), (plateId, 1), (mailId, 0));
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
    public void ServerSlotWriteUnderAPendingEquip_KeepsThePendingEquipAndReturnsTheWrittenItem()
    {
        OpenScreen();
        Equip(armorId, EquipmentIndex.Body);
        ServerWritesSlot(plateId, EquipmentIndex.Body);
        CloseScreen(fromCancel: false);
        Router.RunUntilIdle();
        TestEnvironment.FlushCoalescer();

        AssertEverywhere(EquipmentIndex.Body, armorId, (armorId, 0), (plateId, 2));
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
