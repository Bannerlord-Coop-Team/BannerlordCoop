using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace GameInterface.Services.Kingdoms.Handlers;

/// <summary>
/// Clears the AI peace decline cooldowns whenever a campaign starts, loads, or is left. The gate
/// lives for the whole session container, so without this a cooldown recorded in one campaign would
/// still hold peace proposals back in the next campaign loaded in the same process.
/// </summary>
internal class AiPeaceProposalGateResetHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IAiPeaceProposalGate peaceProposalGate;

    public AiPeaceProposalGateResetHandler(
        IMessageBroker messageBroker,
        IAiPeaceProposalGate peaceProposalGate)
    {
        this.messageBroker = messageBroker;
        this.peaceProposalGate = peaceProposalGate;

        messageBroker.Subscribe<GameLoadStarted>(Handle_Reset);
        messageBroker.Subscribe<CampaignReady>(Handle_Reset);
        messageBroker.Subscribe<MainMenuEntered>(Handle_Reset);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GameLoadStarted>(Handle_Reset);
        messageBroker.Unsubscribe<CampaignReady>(Handle_Reset);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_Reset);
    }

    private void Handle_Reset<T>(MessagePayload<T> payload) where T : IMessage
    {
        peaceProposalGate.ClearDeclineCooldowns();
    }
}
