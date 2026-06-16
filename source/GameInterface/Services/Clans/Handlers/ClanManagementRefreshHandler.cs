using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
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

        currentClanManagementVM = null;
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClanManagementVMCreated>(Handle_ClanManagementVMCreated);
        messageBroker.Unsubscribe<RefreshPartiesList>(Handle_RefreshPartiesList);
    }

    private void Handle_ClanManagementVMCreated(MessagePayload<ClanManagementVMCreated> obj)
    {
        currentClanManagementVM = obj.What.ClanManagementVM;
    }

    private void Handle_RefreshPartiesList(MessagePayload<RefreshPartiesList> obj)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            currentClanManagementVM?.ClanParties?.RefreshPartiesList();
        });
    }
}
