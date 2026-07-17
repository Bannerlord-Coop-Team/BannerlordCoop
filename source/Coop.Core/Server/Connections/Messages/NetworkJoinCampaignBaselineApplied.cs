using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Confirms that a joining client applied both complete campaign baselines.
/// </summary>
[ProtoContract]
public record NetworkJoinCampaignBaselineApplied : ICommand
{
}
