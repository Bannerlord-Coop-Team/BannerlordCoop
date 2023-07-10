using Common.Messaging;
using Coop.Core.Server.Services.MobileParties.Messages;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Handlers
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkEndSettlementEncounter : ICommand
    {
    }
}