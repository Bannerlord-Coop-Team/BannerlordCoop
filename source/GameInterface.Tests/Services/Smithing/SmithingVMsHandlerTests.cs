using Common;
using Common.Messaging;
using Coop.Tests.Mocks;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Handlers;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using Moq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Smithing;

/// <summary>Checks that delayed crafting replies leave another window's pending request untouched.</summary>
public sealed class SmithingVMsHandlerTests
{
    // Starts the shared game-thread pump for filtered test runs.
    static SmithingVMsHandlerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    // Covers both rejected and successful replies belonging to a previously closed window.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OldResult_DoesNotReplaceOrClearNewPendingItem(bool success)
    {
        var pendingItem = (ItemObject)RuntimeHelpers.GetUninitializedObject(typeof(ItemObject));
        pendingItem.StringId = "ClientVisual_new-request";
        var resultItem = (ItemObject)RuntimeHelpers.GetUninitializedObject(typeof(ItemObject));
        var weaponDesign = (WeaponDesignVM)RuntimeHelpers.GetUninitializedObject(typeof(WeaponDesignVM));
        weaponDesign.CraftedItemObject = pendingItem;
        var craftingVM = (CraftingVM)RuntimeHelpers.GetUninitializedObject(typeof(CraftingVM));
        var provider = new Mock<ISmithingVMsProvider>();
        provider.Setup(x => x.GetCurrentWeaponDesignVM()).Returns(weaponDesign);
        provider.Setup(x => x.GetCurrentCraftingVM()).Returns(craftingVM);
        using var broker = new MessageBroker();
        using var handler = new SmithingVMsHandler(broker, Mock.Of<IObjectManager>(), provider.Object);

        broker.Publish(this, new CreateCraftingResultPopup(resultItem, null, success, "old-request"));
        GameThread.Run(() => { }, blocking: true);

        Assert.Same(pendingItem, weaponDesign.CraftedItemObject);
        Assert.False(weaponDesign.IsInFinalCraftingStage);
        Assert.Null(weaponDesign.CraftingResultPopup);
    }
}
