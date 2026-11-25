using Common.Util;
using GameInterface.Services.Inventory.Interfaces;
using Moq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Inventory;

public class InventoryScreenRosterRefresherTests
{
    [Fact]
    public void ReceivedEquipmentSlot_SurvivesTheScreensReset()
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero._battleEquipment = new Equipment(Equipment.EquipmentType.Battle);
        hero._civilianEquipment = new Equipment(Equipment.EquipmentType.Civilian);
        hero._stealthEquipment = new Equipment(Equipment.EquipmentType.Stealth);
        var character = ObjectHelper.SkipConstructor<CharacterObject>();
        character.HeroObject = hero;

        // Vanilla copies each hero's sets when the screen opens, and Reset fills the live sets back from them
        var initialEquipment = ObjectHelper.SkipConstructor<InventoryLogic.PartyEquipment>();
        initialEquipment.CharacterEquipments = new Dictionary<CharacterObject, Equipment[]>
        {
            [character] = new[]
            {
                new Equipment(hero._battleEquipment),
                new Equipment(hero._civilianEquipment),
                new Equipment(hero._stealthEquipment),
            },
        };
        var logic = new InventoryLogic(null, null, null);
        logic._rosters[0] = new ItemRoster();
        logic._rosters[1] = new ItemRoster();
        logic._partyInitialEquipment = initialEquipment;
        var view = new Mock<IInventoryScreenViewInterface>();
        var inventory = new InventoryScreenRosterRefresher(view.Object);
        inventory.CaptureInventoryBaseline(logic);

        var received = new EquipmentElement(new ItemObject("received_armor"));
        Assert.True(inventory.TryApplyEquipmentUpdate(logic, hero._civilianEquipment, (int)EquipmentIndex.Body, received));

        // The player's own pending edit, which Reset should undo
        hero._civilianEquipment._itemSlots[(int)EquipmentIndex.Body] = new EquipmentElement(new ItemObject("pending_armor"));
        logic._partyInitialEquipment.ResetEquipment();

        Assert.Same(received.Item, hero._civilianEquipment[EquipmentIndex.Body].Item);
        Assert.Null(hero._battleEquipment[EquipmentIndex.Body].Item);
        Assert.Null(hero._stealthEquipment[EquipmentIndex.Body].Item);
        view.Verify(v => v.RefreshEquipment(logic), Times.Once);
    }
}
