using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinance;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans;

public class CoopClanWorkshopItemVM : ClanFinanceWorkshopItemVM
{
    [DataSourceProperty]
    public bool CanManageAsset => CoopClanPermissions.CanManageClan(Workshop.Owner?.Clan);

    public CoopClanWorkshopItemVM(Workshop workshop, Action<ClanFinanceWorkshopItemVM> onSelection,
        Action onRefresh, Action<ClanCardSelectionInfo> openPopup)
        : base(workshop, onSelection, onRefresh, openPopup)
    {
    }

    public override void RefreshValues()
    {
        if (CanManageAsset)
        {
            base.RefreshValues();
        }
        else
        {
            // Warehouse rosters belong to the viewing client, so only read the shared workshop itself.
            Name = Workshop.WorkshopType.Name.ToString();
            WorkshopTypeId = Workshop.WorkshopType.StringId;
            Location = Workshop.Settlement.Name.ToString();
            Income = (int)(Workshop.ProfitMade / Campaign.Current.Models.ClanFinanceModel.RevenueSmoothenFraction());
            IncomeValueText = DetermineIncomeText(Income);
            InputsText = GameTexts.FindText("str_clan_workshop_inputs").ToString();
            OutputsText = GameTexts.FindText("str_clan_workshop_outputs").ToString();
            ManageWorkshopHint.HintText = GameTexts.FindText("str_coop_clan_assets_leader_only");
            ItemProperties.Clear();
            PopulateStatsList();
        }

        OnPropertyChanged(nameof(CanManageAsset));
    }
}
