using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.Heroes.Messages;
[ProtoContract(SkipConstructor = true)]
internal class NetworkHeroCreated : IEvent
{
}
