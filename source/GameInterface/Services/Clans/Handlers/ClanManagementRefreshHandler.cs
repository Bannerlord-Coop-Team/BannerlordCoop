using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI;
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

        messageBroker.Subscribe<RefreshPartiesList>(Handle_RefreshPartiesList);
        messageBroker.Subscribe<RefreshWorkshopsList>(Handle_RefreshWorkshopsList);
        messageBroker.Subscribe<RefreshClanMembersList>(Handle_RefreshClanMembersList);
        messageBroker.Subscribe<RefreshAfterRoleAssignment>(Handle_RefreshAfterRoleAssignment);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RefreshPartiesList>(Handle_RefreshPartiesList);
        messageBroker.Unsubscribe<RefreshWorkshopsList>(Handle_RefreshWorkshopsList);
        messageBroker.Unsubscribe<RefreshClanMembersList>(Handle_RefreshClanMembersList);
        messageBroker.Unsubscribe<RefreshAfterRoleAssignment>(Handle_RefreshAfterRoleAssignment);
    }

    private void Handle_RefreshPartiesList(MessagePayload<RefreshPartiesList> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!(ScreenManager.TopScreen is GauntletClanScreen clanScreen)) return;

            clanScreen._dataSource?.ClanParties?.RefreshPartiesList();
            clanScreen._dataSource?.ClanMembers?.RefreshMembersList(); // Needed to refresh clan members who can be party leaders
        }, context: "ClanRefresh.Parties");
    }

    private void Handle_RefreshWorkshopsList(MessagePayload<RefreshWorkshopsList> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!(ScreenManager.TopScreen is GauntletClanScreen clanScreen)) return;

            clanScreen._dataSource?.ClanIncome?.RefreshList();
        }, context: "ClanRefresh.Workshops");
    }

    private void Handle_RefreshClanMembersList(MessagePayload<RefreshClanMembersList> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!(ScreenManager.TopScreen is GauntletClanScreen clanScreen)) return;

            clanScreen._dataSource?.ClanMembers?.RefreshMembersList();
            clanScreen._dataSource?.ClanFiefs?.RefreshAllLists(); // Needed to refresh governors
        }, context: "ClanRefresh.Members");
    }

    private void Handle_RefreshAfterRoleAssignment(MessagePayload<RefreshAfterRoleAssignment> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!(ScreenManager.TopScreen is GauntletClanScreen clanScreen) ||
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
}
