using Common.LogicStates;

namespace Coop.Mod.LogicStates.Server
{
    public interface IServerState : IState
    {
        void StartServer();
        void StopServer();
    }
}
