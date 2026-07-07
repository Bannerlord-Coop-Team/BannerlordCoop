using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents.Messages;

namespace GameInterface.Services.MapEvents.Handlers;

internal class RaidAiInterventionConfigHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public RaidAiInterventionConfigHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkRequestRaidAiInterventionConfigChange>(Handle_NetworkRequestRaidAiInterventionConfigChange);
        messageBroker.Subscribe<NetworkRaidAiInterventionConfigChanged>(Handle_NetworkRaidAiInterventionConfigChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestRaidAiInterventionConfigChange>(Handle_NetworkRequestRaidAiInterventionConfigChange);
        messageBroker.Unsubscribe<NetworkRaidAiInterventionConfigChanged>(Handle_NetworkRaidAiInterventionConfigChanged);
    }

    private void Handle_NetworkRequestRaidAiInterventionConfigChange(MessagePayload<NetworkRequestRaidAiInterventionConfigChange> payload)
    {
        if (ModInformation.IsClient)
            return;

        SetAndBroadcast(payload.What.Allow);
    }

    private void Handle_NetworkRaidAiInterventionConfigChanged(MessagePayload<NetworkRaidAiInterventionConfigChanged> payload)
    {
        if (ModInformation.IsServer)
            return;

        MapEventConfig.AllowRaidAiIntervention = payload.What.Allow;
        messageBroker.Publish(this, new SendInformationMessage(StatusText));
    }

    internal void SetAndBroadcast(bool allow)
    {
        MapEventConfig.AllowRaidAiIntervention = allow;
        network.SendAll(new NetworkRaidAiInterventionConfigChanged(allow));
        messageBroker.Publish(this, new SendInformationMessage(StatusText));
    }

    internal static string StatusText =>
        $"Raid AI intervention is {(MapEventConfig.AllowRaidAiIntervention ? "enabled" : "disabled")}";
}
