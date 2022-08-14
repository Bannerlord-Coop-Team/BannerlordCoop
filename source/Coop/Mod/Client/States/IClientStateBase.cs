using Common.LogicStates;
using System.Threading.Tasks;

namespace Coop.Mod.LogicStates.Client
{
    public interface IClientStateBase : IState
    {
        Task<bool> Connect();
    }
}
