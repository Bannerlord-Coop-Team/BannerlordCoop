using Common.MessageBroker;
using Coop.Mod.GameInterfaces;

namespace Coop.Mod.EventHandlers
{
    public abstract class EventHandlerBase
    {
        protected readonly IMessageBroker _messageBroker;
        protected readonly IGameInterface _gameInterface;

        public EventHandlerBase(ICommunicator communicator)
        {
            _messageBroker = communicator.MessageBroker;
            _gameInterface = communicator.GameInterface;
        }
    }
}