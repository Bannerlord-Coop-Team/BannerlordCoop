using Coop.Communication.MessageBroker;
using Coop.Mod.Messages.Commands;
using Coop.Mod.Messages.Queries;
using GameInterface.Messages.Queries;
using System.Threading.Tasks;

namespace Coop.Mod.LogicStates.Client
{
    internal class InitialClientState : ClientStateBase
    {
        public InitialClientState(IClientLogic logic, IMessageBroker messageBroker, IQueryDispatcher queryDispatcher) : base(logic, messageBroker, queryDispatcher)
        {
        }

        public override Task<bool> Connect()
        {
            var query = new GetPlatformById { Id = "" };
            var platform = QueryDispatcher.Dispatch(query);

            return Task.FromResult(true);
        }
    }
}
