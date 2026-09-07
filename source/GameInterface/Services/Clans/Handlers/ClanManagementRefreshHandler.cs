using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanManagementRefreshHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public ClanManagementRefreshHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkRefreshPartiesList>(Handle_NetworkRefreshPartiesList);
        messageBroker.Subscribe<NetworkRefreshWorkshopsList>(Handle_NetworkRefreshWorkshopsList);
        messageBroker.Subscribe<NetworkRefreshClanMembersList>(Handle_NetworkRefreshClanMembersList);
        messageBroker.Subscribe<NetworkRefreshAfterRoleAssignment>(Handle_NetworkRefreshAfterRoleAssignment);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRefreshPartiesList>(Handle_NetworkRefreshPartiesList);
        messageBroker.Unsubscribe<NetworkRefreshWorkshopsList>(Handle_NetworkRefreshWorkshopsList);
        messageBroker.Unsubscribe<NetworkRefreshClanMembersList>(Handle_NetworkRefreshClanMembersList);
        messageBroker.Unsubscribe<NetworkRefreshAfterRoleAssignment>(Handle_NetworkRefreshAfterRoleAssignment);
    }

    private void Handle_NetworkRefreshPartiesList(MessagePayload<NetworkRefreshPartiesList> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!CheckClanAndTopScreen(obj.What.ClanId, out var clanScreen)) return;

            clanScreen._dataSource?.ClanParties?.RefreshPartiesList();
            clanScreen._dataSource?.ClanMembers?.RefreshMembersList(); // Needed to refresh clan members who can be party leaders
        }, context: "ClanRefresh.Parties");
    }

    private void Handle_NetworkRefreshWorkshopsList(MessagePayload<NetworkRefreshWorkshopsList> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!CheckClanAndTopScreen(obj.What.ClanId, out var clanScreen)) return;

            clanScreen._dataSource?.ClanIncome?.RefreshList();
        }, context: "ClanRefresh.Workshops");
    }

    private void Handle_NetworkRefreshClanMembersList(MessagePayload<NetworkRefreshClanMembersList> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!CheckClanAndTopScreen(obj.What.ClanId, out var clanScreen)) return;

            clanScreen._dataSource?.ClanMembers?.RefreshMembersList();
            clanScreen._dataSource?.ClanFiefs?.RefreshAllLists(); // Needed to refresh governors
        }, context: "ClanRefresh.Members");
    }

    private void Handle_NetworkRefreshAfterRoleAssignment(MessagePayload<NetworkRefreshAfterRoleAssignment> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (ScreenManager.TopScreen is not GauntletClanScreen clanScreen ||
                clanScreen._dataSource == null) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            foreach (var partyItemVM in clanScreen._dataSource.ClanParties._parties)
            {
                if (partyItemVM.Party.IsMobile && partyItemVM.Party.MobileParty == mobileParty)
                {
                    partyItemVM.OnRoleAssigned();
                    break;
                }
            }
        }, context: "ClanRefresh.RoleAssignment");
    }

    private bool CheckClanAndTopScreen(string clanId, out GauntletClanScreen clanScreen)
    {
        clanScreen = ScreenManager.TopScreen as GauntletClanScreen;
        if (clanScreen == null) return false;

        if (!objectManager.TryGetObjectWithLogging<Clan>(clanId, out var clan)) return false;
        if (clan != Clan.PlayerClan) return false;

        return true;
    }
}
