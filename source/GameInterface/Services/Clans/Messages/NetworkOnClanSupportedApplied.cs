using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract]
public readonly struct NetworkOnClanSupportedApplied : ICommand
{
}