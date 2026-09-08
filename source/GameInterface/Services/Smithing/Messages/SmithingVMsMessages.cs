using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct CraftingVMCreated : IEvent
{
    public readonly CraftingVM CraftingVM;

    public CraftingVMCreated(CraftingVM craftingVM)
    {
        CraftingVM = craftingVM;
    }
}

public readonly struct RefreshWeaponDesignVM : IEvent
{
    public readonly Town Town;

    public RefreshWeaponDesignVM(Town town)
    {
        Town = town;
    }
}

public readonly struct CreateCraftingResultPopup : IEvent
{
    public readonly ItemObject CraftedItem;
    public readonly bool Success;
    public readonly string ClientRequestId;

    public CreateCraftingResultPopup(
        ItemObject craftedItem,
        bool success,
        string clientRequestId)
    {
        CraftedItem = craftedItem;
        Success = success;
        ClientRequestId = clientRequestId;
    }
}

public readonly struct RefreshCraftingVM : IEvent {}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRefreshSmelting : ICommand
{
    [ProtoMember(1)]
    public readonly string CraftingHeroId;

    [ProtoMember(2)]
    public readonly EquipmentElement EquipmentElement;

    [ProtoMember(3)]
    public readonly bool SmeltingSucceeded;

    public NetworkRefreshSmelting(string craftingHeroId, EquipmentElement equipmentElement, bool smeltingSucceeded)
    {
        CraftingHeroId = craftingHeroId;
        EquipmentElement = equipmentElement;
        SmeltingSucceeded = smeltingSucceeded;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRefreshRefinement : ICommand
{
    [ProtoMember(1)]
    public readonly string CraftingHeroId;

    public NetworkRefreshRefinement(string craftingHeroId)
    {
        CraftingHeroId = craftingHeroId;
    }
}
