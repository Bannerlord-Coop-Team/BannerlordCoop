using Coop.Core.Server.Connections;

namespace Coop.Core.Server.Connections.States
{
    internal class InitialConnectionState : IConnectionState
    {
        IConnection _connection;

        public InitialConnectionState(IConnection connection)
        {
            _connection = connection;
        }
    }
}