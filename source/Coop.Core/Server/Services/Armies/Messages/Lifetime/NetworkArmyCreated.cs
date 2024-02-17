using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Armies.Messages.Lifetime;

/// <summary>
/// Network event notifying that a party has been created on the client.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkArmyCreated : IEvent
{
}
