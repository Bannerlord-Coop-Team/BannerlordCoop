using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// A player has entered the campaign state
/// </summary>
internal record PlayerCampaignEntered : IEvent
{
}