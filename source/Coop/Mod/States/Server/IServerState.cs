namespace Coop.Mod.States.Server
{
    public interface IServerState : IState
    {
        void StartServer();
        void StopServer();
    }
}
