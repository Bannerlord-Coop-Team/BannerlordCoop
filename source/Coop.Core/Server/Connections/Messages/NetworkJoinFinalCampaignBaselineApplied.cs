using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Confirms that the final complete join baseline is current on the client.
/// </summary>
[ProtoContract]
public record NetworkJoinFinalCampaignBaselineApplied : ICommand
{
}
