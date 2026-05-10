using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Refinement;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;

namespace GameInterface.Services.Smithing.Messages;

public record SmeltingVMCreated : IEvent
{
    public SmeltingVM SmeltingVM;

    public SmeltingVMCreated(SmeltingVM smeltingVM)
    {
        SmeltingVM = smeltingVM;
    }
}

public record RefinementVMCreated : IEvent
{
    public RefinementVM RefinementVM;

    public RefinementVMCreated(RefinementVM refinementVM)
    {
        RefinementVM = refinementVM;
    }
}

public record CraftingVMCreated : IEvent
{
    public CraftingVM CraftingVM;

    public CraftingVMCreated(CraftingVM craftingVM)
    {
        CraftingVM = craftingVM;
    }
}

public record WeaponDesignVMCreated : IEvent
{
    public WeaponDesignVM WeaponDesignVM;

    public WeaponDesignVMCreated(WeaponDesignVM weaponDesignVM)
    {
        WeaponDesignVM = weaponDesignVM;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkRefreshSmelting : ICommand
{
    public NetworkRefreshSmelting()
    {
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkRefreshRefinement : ICommand
{
    [ProtoMember(1)]
    public string CraftingHeroId;

    public NetworkRefreshRefinement(string craftingHeroId)
    {
        CraftingHeroId = craftingHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkRefreshCraftingVM : ICommand
{
    public NetworkRefreshCraftingVM()
    {
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkRefreshWeaponDesignVM : ICommand
{
    public NetworkRefreshWeaponDesignVM()
    {
    }
}