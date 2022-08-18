using Common.Messaging;
using GameInterface;

namespace Coop.Mod.EventHandlers
{
    public abstract class EventHandlerBase
    {
        protected readonly IMessageBroker _messageBroker;
        protected readonly IGameInterface _gameInterface;

        protected EventHandlerBase(IMessageBroker messageBroker, IGameInterface gameInterface)
        {
            _messageBroker = messageBroker;
            _gameInterface = gameInterface;
        }
    }
}