using Common.LogicStates;

namespace Coop.Core.Client.States
{
    public interface IClientLogic : ILogic, IClientState
    {
        IClientState State { get; set; }
    }
}