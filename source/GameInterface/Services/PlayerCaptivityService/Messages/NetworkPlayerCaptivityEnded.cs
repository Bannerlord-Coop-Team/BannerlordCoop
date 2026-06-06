using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

[ProtoContract]
internal readonly struct NetworkPlayerCaptivityEnded : IEvent
{
}
