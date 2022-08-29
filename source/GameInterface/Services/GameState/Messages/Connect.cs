using Common.Messaging;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct Connect : ICommand
    {
    }

    public readonly struct Connected : ICommand
    {
        public Connected(bool clientPartyExists)
        {
            ClientPartyExists = clientPartyExists;
        }

        public bool ClientPartyExists { get; }
    }
}
