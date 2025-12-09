using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Messages;

namespace GameInterface.Services.GameState.Handlers;

internal class EnterMissionHandler : IHandler
{
    private readonly IGameStateInterface gameStateInterface;
    private readonly IMessageBroker messageBroker;

    public EnterMissionHandler(IGameStateInterface gameStateInterface, IMessageBroker messageBroker)
    {
        this.gameStateInterface = gameStateInterface;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<EnterMissionState>(Handle_EnterMissionState);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<EnterMissionState>(Handle_EnterMissionState);
    }

    private void Handle_EnterMissionState(MessagePayload<EnterMissionState> obj)
    {
        messageBroker.Publish(this, new MissionStateEntered());
    }
}
