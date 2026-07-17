using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Requests another complete time and mobile-party baseline while joining.
/// </summary>
[ProtoContract]
public record NetworkJoinCampaignBaselineRequested : ICommand
{
}
