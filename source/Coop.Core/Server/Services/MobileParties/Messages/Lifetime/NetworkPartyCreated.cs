using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages.Lifetime;

[ProtoContract(SkipConstructor = true)]
internal class NetworkPartyCreated : IEvent
{
}
