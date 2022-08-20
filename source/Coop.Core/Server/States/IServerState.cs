using Common.LogicStates;
using System;

namespace Coop.Core.Server.States
{
    public interface IServerState : IState, IDisposable
    {
        void Start();
        void Stop();
    }
}
