using Autofac;
using GameInterface;
using LiteNetLib;

namespace Coop.Core.Server.Connections
{
    public interface IConnectionLogicFactory
    {
        IConnectionLogic CreateLogic(NetPeer netPeer);
    }
    internal class ConnectionLogicFactory : IConnectionLogicFactory
    {
        private IContainer Container => containerProvider.GetContainer();
        private readonly IContainerProvider containerProvider;

        public ConnectionLogicFactory(IContainerProvider containerProvider)
        {
            this.containerProvider = containerProvider;
        }

        public IConnectionLogic CreateLogic(NetPeer netPeer)
        {
            return Container.Resolve<IConnectionLogic>(new NamedParameter("playerId", netPeer));
        }
    }
}
