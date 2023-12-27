using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Sync.Messages
{
    /// <summary>
    /// Sent to the server to inform it about client's synchronization state.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkSyncStatus : IMessage
    {
        [ProtoMember(1)]
        public bool Synchronized { get; }

        public NetworkSyncStatus(bool synchronized)
        {
            Synchronized = synchronized;
        }
    }
}
