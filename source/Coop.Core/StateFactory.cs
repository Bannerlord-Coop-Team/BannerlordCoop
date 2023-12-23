using Autofac;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server;
using Coop.Core.Server.Connections;
using Coop.Core.Server.States;

namespace Coop.Core
{
    public interface IStateFactory
    {
        TState CreateConnectionState<TState>(IConnectionLogic connectionLogic) where TState : IConnectionState;
        TState CreateServerState<TState>(IServerLogic serverLogic) where TState : IServerState;
        TState CreateClientState<TState>(IClientLogic clientLogic) where TState : IClientState;
    }

    internal class StateFactory : IStateFactory
    {
        private IContainer Container => containerProvider.GetContainer();

        private IContainerProvider containerProvider;

        public StateFactory(IContainerProvider containerProvider)
        {
            this.containerProvider = containerProvider;
        }

        public TState CreateConnectionState<TState>(IConnectionLogic connectionLogic) where TState : IConnectionState
        {
            return Container.Resolve<TState>(new TypedParameter(typeof(IConnectionLogic), connectionLogic));
        }

        public TState CreateServerState<TState>(IServerLogic serverLogic) where TState : IServerState
        {
            return Container.Resolve<TState>(new TypedParameter(typeof(IServerLogic), serverLogic));
        }

        public TState CreateClientState<TState>(IClientLogic clientLogic) where TState : IClientState
        {
            return Container.Resolve<TState>(new TypedParameter(typeof(IClientLogic), clientLogic));
        }
    }
}
