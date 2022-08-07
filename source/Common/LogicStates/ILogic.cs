using Common.Components;

namespace Common.LogicStates
{
    public interface ILogic : IComponent
    {
        IState State { get; set; }
    }
}
