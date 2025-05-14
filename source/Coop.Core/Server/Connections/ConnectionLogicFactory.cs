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

        public IConnectionLogic CreateLogic(NetPeer netPeer)
        {
            return ContainerProvider.Container.Resolve<IConnectionLogic>(new NamedParameter("playerId", netPeer));
        }
    }
}
