namespace Coop.Mod.Server.Connections
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