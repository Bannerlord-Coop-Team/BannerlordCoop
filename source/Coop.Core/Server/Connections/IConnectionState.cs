using Common.LogicStates;

namespace Coop.Core.Server.Connections
{
    public interface IConnectionState : IState
    {
        /// <summary>
        /// Player joins the server instance
        /// </summary>
        void Join();

        /// <summary>
        /// Player loading server data
        /// </summary>
        void Load();

        /// <summary>
        /// Player entering in enter campaign
        /// </summary>
        void EnterCampaign();

        /// <summary>
        /// Player entering mission state
        /// </summary>
        void EnterMission();
    }
}
