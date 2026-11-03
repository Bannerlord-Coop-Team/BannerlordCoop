using Common;
using Common.Messaging;
using Coop.Tests.Mocks;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Handlers;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using GameInterface.Services.Smithing.Patches;
using HarmonyLib;
using Moq;
using System.Linq;
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

    [Fact]
    public void FinalizedScreen_IsDroppedSoALaterOrderRefreshSkipsIt()
    {
        var craftingVM = (CraftingVM)RuntimeHelpers.GetUninitializedObject(typeof(CraftingVM));
        var provider = new SmithingVMsProvider();
        using var broker = new MessageBroker();
        using var handler = new SmithingVMsHandler(broker, Mock.Of<IObjectManager>(), provider);
        broker.Publish(this, new CraftingVMCreated(craftingVM));

        broker.Publish(this, new CraftingVMFinalized(craftingVM));

        Assert.Null(provider.GetCurrentCraftingVM());
        Assert.Null(provider.GetCurrentWeaponDesignVM());
    }

    [Fact]
    public void FinalizingAnOldScreen_KeepsTheOpenOne()
    {
        var oldVM = (CraftingVM)RuntimeHelpers.GetUninitializedObject(typeof(CraftingVM));
        var openVM = (CraftingVM)RuntimeHelpers.GetUninitializedObject(typeof(CraftingVM));
        var provider = new SmithingVMsProvider();
        using var broker = new MessageBroker();
        using var handler = new SmithingVMsHandler(broker, Mock.Of<IObjectManager>(), provider);
        broker.Publish(this, new CraftingVMCreated(oldVM));
        broker.Publish(this, new CraftingVMCreated(openVM));

        broker.Publish(this, new CraftingVMFinalized(oldVM));

        Assert.Same(openVM, provider.GetCurrentCraftingVM());
    }

    [Fact]
    public void ClosingTheScreen_ThroughTheFinalizePatch_DropsIt()
    {
        var postfix = typeof(CraftingVMConstructorPatch).GetMethod(nameof(CraftingVMConstructorPatch.OnFinalizePostfix));
        var target = Assert.Single(postfix.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>());
        Assert.Equal(nameof(CraftingVM.OnFinalize), target.info.methodName);
        Assert.NotEmpty(postfix.GetCustomAttributes(typeof(HarmonyPostfix), false));

        var craftingVM = (CraftingVM)RuntimeHelpers.GetUninitializedObject(typeof(CraftingVM));
        var provider = new SmithingVMsProvider();
        using var handler = new SmithingVMsHandler(MessageBroker.Instance, Mock.Of<IObjectManager>(), provider);
        MessageBroker.Instance.Publish(this, new CraftingVMCreated(craftingVM));
        Assert.Same(craftingVM, provider.GetCurrentCraftingVM());

        CraftingVMConstructorPatch.OnFinalizePostfix(craftingVM);

        Assert.Null(provider.GetCurrentCraftingVM());
    }
}
