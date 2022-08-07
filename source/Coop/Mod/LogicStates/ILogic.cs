using Common.Components;

namespace Coop.Mod.LogicStates
{
    public interface ILogic : IComponent
    {
        ICommunicator Communicator { get; }
        IState State { get; set; }
    }
}
