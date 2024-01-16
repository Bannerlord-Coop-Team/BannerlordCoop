using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// A player has requested save data over the network
/// </summary>
[ProtoContract]
public record NetworkRequestSaveData : ICommand
{
}
