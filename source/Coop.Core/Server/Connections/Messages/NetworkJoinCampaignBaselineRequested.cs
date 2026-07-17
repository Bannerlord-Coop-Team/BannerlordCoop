using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Requests a fresher reliable time and party-position baseline while joining.
/// </summary>
[ProtoContract]
public record NetworkJoinCampaignBaselineRequested : ICommand
{
}
