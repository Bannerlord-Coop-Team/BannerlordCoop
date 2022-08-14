using Coop.Communication.MessageBroker;
using Coop.Mod.Messages.Queries;
using GameInterface.Messages.Queries;
using System.Threading.Tasks;

namespace Coop.Mod.LogicStates.Client
{
    public abstract class ClientStateBase : IClientStateBase
    {
        protected readonly IClientLogic Logic;
        protected readonly IMessageBroker MessageBroker;
        protected readonly IQueryDispatcher QueryDispatcher;

        public ClientStateBase(IClientLogic logic, IMessageBroker messageBroker, IQueryDispatcher queryDispatcher)
        {
            Logic = logic;
            MessageBroker = messageBroker;
            QueryDispatcher = queryDispatcher;
        }


        public abstract Task<bool> Connect();
    }
}
