using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinance;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans;

public class SharedClanAlleyItemVM : ClanFinanceAlleyItemVM
{
    [DataSourceProperty]
    public bool CanManageAsset => SharedClanPermissions.CanManageClan(Alley.Owner?.Clan);

    public SharedClanAlleyItemVM(Alley alley, Action<ClanCardSelectionInfo> openPopup,
        Action<ClanFinanceAlleyItemVM> onSelection, Action onRefresh)
        : base(alley, openPopup, onSelection, onRefresh)
    {
    }

    public override void RefreshValues()
    {
        base.RefreshValues();
        if (!CanManageAsset) ManageAlleyHint.HintText = GameTexts.FindText("str_coop_clan_assets_leader_only");
        OnPropertyChanged(nameof(CanManageAsset));
    }
}
