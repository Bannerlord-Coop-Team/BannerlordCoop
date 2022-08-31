using Common.LogicStates;
using Common.Messaging;

namespace Coop.Core.Server.States
{
    public interface IServerLogic : ILogic, IServerState
    {
        IServerState State { get; set; }
        ICoopServer NetworkServer { get; }
    }
}