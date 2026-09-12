using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Refinement;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;

namespace GameInterface.Services.Smithing.Interfaces;

public interface ISmithingVMsProvider : IGameAbstraction
{
    void SetCurrentSmeltingVM(SmeltingVM smeltingVM);
    void SetCurrentRefinementVM(RefinementVM refinementVM);
    void SetCurrentCraftingVM(CraftingVM craftingVM);
    void SetCurrentWeaponDesignVM(WeaponDesignVM weaponDesignVM);

    SmeltingVM GetCurrentSmeltingVM();
    RefinementVM GetCurrentRefinementVM();
    CraftingVM GetCurrentCraftingVM();
    WeaponDesignVM GetCurrentWeaponDesignVM();

    CraftingOrder GetActiveCraftingOrder();
}

public class SmithingVMsProvider : ISmithingVMsProvider
{
    private SmeltingVM currentSmeltingVM = null;
    private RefinementVM currentRefinementVM = null;
    private CraftingVM currentCraftingVM = null;
    private WeaponDesignVM currentWeaponDesignVM = null;

    public void SetCurrentSmeltingVM(SmeltingVM smeltingVM)
    {
        currentSmeltingVM = smeltingVM;
    }

    public void SetCurrentRefinementVM(RefinementVM refinementVM)
    {
        currentRefinementVM = refinementVM;
    }

    public void SetCurrentCraftingVM(CraftingVM craftingVM)
    {
        currentCraftingVM = craftingVM;
    }

    public void SetCurrentWeaponDesignVM(WeaponDesignVM weaponDesignVM)
    {
        currentWeaponDesignVM = weaponDesignVM;
    }

    public SmeltingVM GetCurrentSmeltingVM()
    {
        return currentSmeltingVM;
    }

    public RefinementVM GetCurrentRefinementVM()
    {
        return currentRefinementVM;
    }

    public CraftingVM GetCurrentCraftingVM()
    {
        return currentCraftingVM;
    }

    public WeaponDesignVM GetCurrentWeaponDesignVM()
    {
        return currentWeaponDesignVM;
    }

    public CraftingOrder GetActiveCraftingOrder()
    {
        if (currentWeaponDesignVM == null) return null;

        return currentWeaponDesignVM.ActiveCraftingOrder?.CraftingOrder;
    }
}