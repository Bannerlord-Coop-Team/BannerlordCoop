using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Refinement;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;

namespace GameInterface.Services.Smithing.Interfaces;

public interface ISmithingVMsProvider : IGameAbstraction
{
    void SetCurrentCraftingVM(CraftingVM craftingVM);
    void ClearCurrentCraftingVM(CraftingVM craftingVM);

    SmeltingVM GetCurrentSmeltingVM();
    RefinementVM GetCurrentRefinementVM();
    CraftingVM GetCurrentCraftingVM();
    WeaponDesignVM GetCurrentWeaponDesignVM();

    CraftingOrder GetActiveCraftingOrder();
}

public class SmithingVMsProvider : ISmithingVMsProvider
{
    private CraftingVM currentCraftingVM = null;

    public void SetCurrentCraftingVM(CraftingVM craftingVM)
    {
        currentCraftingVM = craftingVM;
    }

    public void ClearCurrentCraftingVM(CraftingVM craftingVM)
    {
        // A newer screen may already have replaced the finalized one
        if (ReferenceEquals(currentCraftingVM, craftingVM))
            currentCraftingVM = null;
    }

    public SmeltingVM GetCurrentSmeltingVM()
    {
        return currentCraftingVM?.Smelting;
    }

    public RefinementVM GetCurrentRefinementVM()
    {
        return currentCraftingVM?.Refinement;
    }

    public CraftingVM GetCurrentCraftingVM()
    {
        return currentCraftingVM;
    }

    public WeaponDesignVM GetCurrentWeaponDesignVM()
    {
        return currentCraftingVM?.WeaponDesign;
    }

    public CraftingOrder GetActiveCraftingOrder()
    {
        return currentCraftingVM?.WeaponDesign?.ActiveCraftingOrder?.CraftingOrder;
    }
}