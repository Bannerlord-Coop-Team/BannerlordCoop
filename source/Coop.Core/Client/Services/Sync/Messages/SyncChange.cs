using Common.Messaging;

namespace Coop.Core.Client.Services.Sync.Messages
{
    /// <summary>
    /// Called when synchronization status on the client has been changed.
    /// </summary>
    internal class SyncChange : IEvent
    {
        public bool Synchronized { get; }

        public SyncChange(bool synchronized)
        {
            Synchronized = synchronized;
        }
    }
}
