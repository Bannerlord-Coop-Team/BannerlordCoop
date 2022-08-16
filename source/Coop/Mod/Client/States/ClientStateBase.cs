using Coop.Communication.MessageBroker;
using GameInterface;
using System.Threading.Tasks;

namespace Coop.Mod.LogicStates.Client
{
    public abstract class ClientStateBase : IClientStateBase
    {
        protected readonly IClientLogic Logic;
        protected readonly IMessageBroker MessageBroker;
        protected readonly IGameInterface GameInterface;

        public ClientStateBase(IClientLogic logic, IMessageBroker messageBroker, IGameInterface gameInterface)
        {
            Logic = logic;
            MessageBroker = messageBroker;
            GameInterface = gameInterface;
        }
    }
}
