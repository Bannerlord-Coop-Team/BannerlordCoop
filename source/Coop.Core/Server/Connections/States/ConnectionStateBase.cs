using Common.Messaging;
using Common.Network;

namespace Coop.Core.Server.Connections.States
{
    /// <summary>
    /// Setup for a given ConnectionState
    /// </summary>
    public abstract class ConnectionStateBase : IConnectionState
    {
        public IConnectionLogic ConnectionLogic { get; }

        public ConnectionStateBase(IConnectionLogic connectionLogic)
        {
            ConnectionLogic = connectionLogic;
        }

        public abstract void CreateCharacter();
        public abstract void TransferSave();
        public abstract void Load();
        public abstract void EnterCampaign();
        public abstract void EnterMission();
        public abstract void Dispose();
    }
}
