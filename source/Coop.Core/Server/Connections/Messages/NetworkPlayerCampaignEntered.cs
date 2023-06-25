using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Network event when a player has entered the CampaignState
/// </summary>
[ProtoContract]
public record NetworkPlayerCampaignEntered : IEvent
{
}
