using Common.Messaging;
using GameInterface.Services.Clans;
using NavalDLC.ViewModelCollection.ClanManagement;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;

namespace Coop.Naval.Clans;

/// <summary>
/// Coop clan screen with the naval fiefs tab from NavalClanManagementVM.
/// </summary>
public class CoopNavalClanManagementVM : CoopClanManagementVM
{
    public CoopNavalClanManagementVM(Action onClose, Action<Hero> showHeroOnMap, Action<Hero> openParty,
        Action openBannerEditor, IClanFinance finance, IMessageBroker messages)
        : base(onClose, showHeroOnMap, openParty, openBannerEditor, finance, messages)
    {
    }

    // Adds the shipyard and coastal patrol rows to settlements
    public override ClanFiefsVM CreateFiefsDataSource(Action onRefresh, Action<ClanCardSelectionInfo> openCardSelectionPopup)
    {
        return new NavalClanFiefsVM(onRefresh, openCardSelectionPopup);
    }
}
