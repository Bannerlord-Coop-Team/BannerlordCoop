using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Incidents.Interfaces;
using GameInterface.Services.Incidents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Incidents.Handlers;

internal class IncidentsCampaignBehaviourHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<IncidentsCampaignBehaviourHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IIncidentsCampaignBehaviourInterface incidentsCampaignBehaviourInterface;

    public IncidentsCampaignBehaviourHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IIncidentsCampaignBehaviourInterface incidentsCampaignBehaviourInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.incidentsCampaignBehaviourInterface = incidentsCampaignBehaviourInterface;

        messageBroker.Subscribe<IncidentsGameMenuOptionSelected>(Handle_IncidentsGameMenuOptionSelected);
        messageBroker.Subscribe<NetworkIncidentsGameMenuOptionSelected>(Handle_NetworkIncidentsGameMenuOptionSelected);

        messageBroker.Subscribe<IncidentsGameMenuOpened>(Handle_IncidentsGameMenuOpened);
        messageBroker.Subscribe<NetworkIncidentsGameMenuOpened>(Handle_NetworkIncidentsGameMenuOpened);

        messageBroker.Subscribe<IncidentsConversationEnded>(Handle_IncidentsConversationEnded);
        messageBroker.Subscribe<NetworkIncidentsConversationEnded>(Handle_NetworkIncidentsConversationEnded);

        messageBroker.Subscribe<PlayerHeirSelectionCompleted>(Handle_PlayerHeirSelectionCompleted);

        messageBroker.Subscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
        messageBroker.Subscribe<NetworkInitializeServerIncidentsDataKeys>(Handle_NetworkInitializeServerIncidentsDataKeys);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<IncidentsGameMenuOptionSelected>(Handle_IncidentsGameMenuOptionSelected);
        messageBroker.Unsubscribe<NetworkIncidentsGameMenuOptionSelected>(Handle_NetworkIncidentsGameMenuOptionSelected);

        messageBroker.Unsubscribe<IncidentsGameMenuOpened>(Handle_IncidentsGameMenuOpened);
        messageBroker.Unsubscribe<NetworkIncidentsGameMenuOpened>(Handle_NetworkIncidentsGameMenuOpened);

        messageBroker.Unsubscribe<IncidentsConversationEnded>(Handle_IncidentsConversationEnded);
        messageBroker.Unsubscribe<NetworkIncidentsConversationEnded>(Handle_NetworkIncidentsConversationEnded);

        messageBroker.Unsubscribe<PlayerHeirSelectionCompleted>(Handle_PlayerHeirSelectionCompleted);

        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
        messageBroker.Unsubscribe<NetworkInitializeServerIncidentsDataKeys>(Handle_NetworkInitializeServerIncidentsDataKeys);
    }

    private void Handle_IncidentsGameMenuOptionSelected(MessagePayload<IncidentsGameMenuOptionSelected> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.MainParty, out var mainPartyId)) return;

        var message = new NetworkIncidentsGameMenuOptionSelected(mainHeroId, mainPartyId, data.GameMenuId, data.OptionId);
        network.SendAll(message);
    }

    private void Handle_NetworkIncidentsGameMenuOptionSelected(MessagePayload<NetworkIncidentsGameMenuOptionSelected> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryResolvePlayer(obj.Who, data.MainHeroId, data.MainPartyId, out var player, out var mainHero, out var mainParty)) return;
            if (!TryGetGameMenu(data.GameMenuId, out var gameMenu)) return;
            if (!gameMenu.MenuOptions.Any(option => option.IdString == data.OptionId))
            {
                Logger.Warning("Received unknown option {OptionId} for game menu {GameMenuId}", data.OptionId, data.GameMenuId);
                return;
            }

            incidentsCampaignBehaviourInterface.OnGameMenuOptionSelected(player, mainParty, mainHero, data.GameMenuId, data.OptionId);
        });
    }

    private void Handle_IncidentsGameMenuOpened(MessagePayload<IncidentsGameMenuOpened> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.MainParty, out var mainPartyId)) return;

        var message = new NetworkIncidentsGameMenuOpened(mainHeroId, mainPartyId, data.MenuContextId);
        network.SendAll(message);
    }

    private void Handle_NetworkIncidentsGameMenuOpened(MessagePayload<NetworkIncidentsGameMenuOpened> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryResolvePlayer(obj.Who, data.MainHeroId, data.MainPartyId, out var player, out var mainHero, out var mainParty)) return;
            if (!TryGetGameMenu(data.MenuContextId, out _)) return;

            incidentsCampaignBehaviourInterface.OnGameMenuOpened(player, mainParty, mainHero, data.MenuContextId);
        });
    }

    private void Handle_IncidentsConversationEnded(MessagePayload<IncidentsConversationEnded> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.MainParty, out var mainPartyId)) return;

        var message = new NetworkIncidentsConversationEnded(mainHeroId, mainPartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkIncidentsConversationEnded(MessagePayload<NetworkIncidentsConversationEnded> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryResolvePlayer(obj.Who, data.MainHeroId, data.MainPartyId, out var player, out var mainHero, out var mainParty)) return;

            incidentsCampaignBehaviourInterface.ConversationEnded(player, mainParty, mainHero);
        });
    }

    // Runs after heir is accepted and CoopSession data has been migrated
    private void Handle_PlayerHeirSelectionCompleted(MessagePayload<PlayerHeirSelectionCompleted> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.PlayerHero, out var playerHeroId)) return;

        incidentsCampaignBehaviourInterface.OnHeirSelectionOver(playerHeroId);
    }

    private void Handle_PlayerHeroChanged(MessagePayload<PlayerHeroChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out var playerHeroId)) return;

        network.SendAll(new NetworkInitializeServerIncidentsDataKeys(playerHeroId));
    }

    private void Handle_NetworkInitializeServerIncidentsDataKeys(MessagePayload<NetworkInitializeServerIncidentsDataKeys> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.PlayerHeroId)) return;

            incidentsCampaignBehaviourInterface.AddPlayerKeys(data.PlayerHeroId);
        });
    }

    private bool TryResolvePlayer(object sender, string mainHeroId, string mainPartyId, out Player player, out Hero mainHero, out MobileParty mainParty)
    {
        player = null;
        mainHero = null;
        mainParty = null;

        if (!playerManager.IsSenderControllingHero(sender, mainHeroId)) return false;
        if (!playerManager.IsSenderControllingParty(sender, mainPartyId)) return false;

        if (sender is not NetPeer peer) return false;
        if (!playerManager.TryGetPlayer(peer, out player)) return false;

        if (!objectManager.TryGetObjectWithLogging(mainHeroId, out mainHero)) return false;
        if (!objectManager.TryGetObjectWithLogging(mainPartyId, out mainParty)) return false;

        return true;
    }

    private bool TryGetGameMenu(string gameMenuId, out GameMenu gameMenu)
    {
        // GetGameMenu throws on a null key
        gameMenu = string.IsNullOrEmpty(gameMenuId) ? null : Campaign.Current.GameMenuManager.GetGameMenu(gameMenuId);
        if (gameMenu != null) return true;

        Logger.Warning("Received unknown game menu {GameMenuId}", gameMenuId);
        return false;
    }
}
