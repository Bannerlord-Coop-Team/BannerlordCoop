using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Sync
{

    [ProtoContract(SkipConstructor = true)]
    public class NetworkSyncWait : IMessage
    {

    }
}
