using Common.LogicStates;

namespace Coop.Mod.LogicStates.Client
{
    public interface IClientState : IState
    {
        void Connect();
    }
}
