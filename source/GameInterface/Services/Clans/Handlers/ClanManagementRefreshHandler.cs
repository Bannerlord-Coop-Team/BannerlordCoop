using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanManagementRefreshHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanManagementRefreshHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    private ClanManagementVM currentClanManagementVM;

    public ClanManagementRefreshHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ClanManagementVMCreated>(Handle_ClanManagementVMCreated);
        messageBroker.Subscribe<RefreshPartiesList>(Handle_RefreshPartiesList);
        messageBroker.Subscribe<RefreshWorkshopsList>(Handle_RefreshWorkshopsList);
        messageBroker.Subscribe<RefreshClanMembersList>(Handle_RefreshClanMembersList);
        messageBroker.Subscribe<RefreshAfterRoleAssignment>(Handle_RefreshAfterRoleAssignment);

        currentClanManagementVM = null;
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClanManagementVMCreated>(Handle_ClanManagementVMCreated);
        messageBroker.Unsubscribe<RefreshPartiesList>(Handle_RefreshPartiesList);
        messageBroker.Unsubscribe<RefreshWorkshopsList>(Handle_RefreshWorkshopsList);
        messageBroker.Unsubscribe<RefreshClanMembersList>(Handle_RefreshClanMembersList);
        messageBroker.Unsubscribe<RefreshAfterRoleAssignment>(Handle_RefreshAfterRoleAssignment);
    }

    private void Handle_ClanManagementVMCreated(MessagePayload<ClanManagementVMCreated> obj)
    {
        currentClanManagementVM = obj.What.ClanManagementVM;
    }

    private void Handle_RefreshPartiesList(MessagePayload<RefreshPartiesList> obj)
    {
        GameThread.RunSafe(() =>
        {
            currentClanManagementVM?.ClanParties?.RefreshPartiesList();
            currentClanManagementVM?.ClanMembers?.RefreshMembersList(); // Needed to refresh clan members who can be party leaders
        });
    }

    private void Handle_RefreshWorkshopsList(MessagePayload<RefreshWorkshopsList> obj)
    {
        GameThread.RunSafe(() =>
        {
            currentClanManagementVM?.ClanIncome?.RefreshList();
        });
    }

    private void Handle_RefreshClanMembersList(MessagePayload<RefreshClanMembersList> obj)
    {
        GameThread.RunSafe(() =>
        {
            currentClanManagementVM?.ClanMembers?.RefreshMembersList();
            currentClanManagementVM?.ClanFiefs?.RefreshAllLists(); // Needed to refresh governors
        });
    }

    private void Handle_RefreshAfterRoleAssignment(MessagePayload<RefreshAfterRoleAssignment> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            if (currentClanManagementVM == null) return;

            foreach (var partyItemVM in currentClanManagementVM.ClanParties._parties)
            {
                if (partyItemVM.Party.IsMobile && partyItemVM.Party.MobileParty == mobileParty)
                {
                    partyItemVM.OnRoleAssigned();
                    break;
                }
            }
        });
    }
}
