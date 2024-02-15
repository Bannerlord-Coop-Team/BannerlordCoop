using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.Heroes.Messages;

/// <summary>
/// Event that responds to a hero being created.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkHeroCreated : IEvent
{
}
