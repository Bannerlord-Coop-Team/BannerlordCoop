using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Encyclopedia;

[EncyclopediaViewModel(typeof(Settlement))]
public class CoopEncyclopediaSettlementPageVM : EncyclopediaSettlementPageVM
{
    private MBBindingList<TownManagementShopItemVM> workshops;
    private bool hasWorkshops;

    [DataSourceProperty]
    public MBBindingList<TownManagementShopItemVM> Workshops
    {
        get => workshops;
        set
        {
            if (value == workshops) return;

            workshops = value;
            OnPropertyChangedWithValue(value, nameof(Workshops));
        }
    }

    [DataSourceProperty]
    public bool HasWorkshops
    {
        get => hasWorkshops;
        set
        {
            if (value == hasWorkshops) return;

            hasWorkshops = value;
            OnPropertyChangedWithValue(value, nameof(HasWorkshops));
        }
    }

    public CoopEncyclopediaSettlementPageVM(EncyclopediaPageArgs args)
        : base(args)
    {
        Workshops = new MBBindingList<TownManagementShopItemVM>();
        RefreshWorkshops();
    }

    public override void Refresh()
    {
        base.Refresh();
        if (Workshops == null) return;

        RefreshWorkshops();
    }

    private void RefreshWorkshops()
    {
        Workshops.Clear();

        if (!_settlement.IsTown || _settlement.Town?.Workshops == null)
        {
            HasWorkshops = false;
            return;
        }

        foreach (Workshop workshop in _settlement.Town.Workshops)
        {
            WorkshopType workshopType = workshop?.WorkshopType;
            if (workshopType == null || workshopType.IsHidden) continue;

            Workshops.Add(new TownManagementShopItemVM(workshop));
        }

        HasWorkshops = Workshops.Count > 0;
    }
}
