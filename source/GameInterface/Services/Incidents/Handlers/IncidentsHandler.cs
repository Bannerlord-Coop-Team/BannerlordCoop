using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Incidents.Interfaces;
using GameInterface.Services.Incidents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Notifications.Messages;
using LiteNetLib;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Incidents.Handlers;

internal class IncidentsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<IncidentsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IIncidentsInterface incidentsInterface;
    private readonly IIncidentsCampaignBehaviourInterface incidentsCampaignBehaviourInterface;
    private readonly ISessionIncidentsPlayerDataInterface sessionIncidentsPlayerDataInterface;

    public IncidentsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IIncidentsInterface incidentsInterface,
        IIncidentsCampaignBehaviourInterface incidentsCampaignBehaviourInterface,
        ISessionIncidentsPlayerDataInterface sessionIncidentsPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.incidentsInterface = incidentsInterface;
        this.incidentsCampaignBehaviourInterface = incidentsCampaignBehaviourInterface;
        this.sessionIncidentsPlayerDataInterface = sessionIncidentsPlayerDataInterface;

        messageBroker.Subscribe<IncidentOptionInvoked>(Handle_IncidentOptionInvoked);
        messageBroker.Subscribe<NetworkIncidentOptionInvoked>(Handle_NetworkIncidentOptionInvoked);

        messageBroker.Subscribe<NetworkIncidentInvoked>(Handle_NetworkIncidentInvoked);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<IncidentOptionInvoked>(Handle_IncidentOptionInvoked);
        messageBroker.Unsubscribe<NetworkIncidentOptionInvoked>(Handle_NetworkIncidentOptionInvoked);

        messageBroker.Unsubscribe<NetworkIncidentInvoked>(Handle_NetworkIncidentInvoked);
    }

    private void Handle_IncidentOptionInvoked(MessagePayload<IncidentOptionInvoked> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Incident, out var incidentId)) return;

        var message = new NetworkIncidentOptionInvoked(mainHeroId, mainPartyId, incidentId, data.OptionTextId);
        network.SendAll(message);
    }

    private void Handle_NetworkIncidentOptionInvoked(MessagePayload<NetworkIncidentOptionInvoked> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;
            if (!playerManager.IsSenderControllingParty(obj.Who, data.MainPartyId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId, out var mainParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Incident>(data.IncidentId, out var incident)) return;

            if (!sessionIncidentsPlayerDataInterface.TryConsumePendingIncident(data.MainHeroId, data.IncidentId))
            {
                Logger.Warning("Ignoring option for incident {IncidentId} because it isn't the pending incident for hero {HeroId}", data.IncidentId, data.MainHeroId);
                return;
            }

            var selectedOption = incident._options.FirstOrDefault(o => o.Text.GetID() == data.OptionTextId);
            if (selectedOption.Text == null)
            {
                Logger.Warning($"Ignoring incident option invocation because the selected option is invalid.");
                return;
            }

            long activeIncidentSeed = sessionIncidentsPlayerDataInterface.GetActiveIncidentSeed(data.MainHeroId);
            bool isOptionApplied = incidentsInterface.TryInvokeOption(mainHero, mainParty, selectedOption, activeIncidentSeed, out var outcomes);

            // Called outside of InvokeOption to send hero and incident ids directly
            incidentsCampaignBehaviourInterface.OnIncidentResolved(data.MainHeroId, data.IncidentId, incident.Cooldown);

            if (!isOptionApplied)
            {
                Logger.Warning("Ignoring option {OptionTextId} for incident {IncidentId} because its conditions no longer hold", data.OptionTextId, data.IncidentId);
                return;
            }

            // Send outcome quick information messages to display on client
            var message = new NetworkNotifyIncidentOutcomes(outcomes);
            network.Send(obj.Who as NetPeer, message);
        });
    }

    private void Handle_NetworkIncidentInvoked(MessagePayload<NetworkIncidentInvoked> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Drop an invoked incident if in a conversation client-side since the server can't see this
            if (Campaign.Current.ConversationManager.IsConversationFlowActive) return;

            if (!objectManager.TryGetObjectWithLogging<Incident>(data.IncidentId, out var incident)) return;

            incidentsInterface.PrepareIncidentText(incident, data.ActiveIncidentSeed);

            MapState mapState = GameStateManager.Current.LastOrDefault<MapState>();
            if (mapState != null)
            {
                mapState.NextIncident = incident;
            }
        });
    }
}
