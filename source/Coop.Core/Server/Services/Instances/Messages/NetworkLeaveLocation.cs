using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Instances.Messages;

/// <summary>
/// Sent by a client when it leaves its interior location (the mission ends). Lets the server release
/// the client's P2P instance membership immediately — re-electing the host if needed — instead of
/// waiting for the indirect triggers (<see cref="Connections.Messages.NetworkPlayerCampaignEntered"/>
/// on campaign re-entry, or a disconnect). The server resolves the instance from the sending peer, so
/// no payload is needed.
/// </summary>
[ProtoContract]
public record NetworkLeaveLocation : ICommand
{
}
