namespace Coop.Core.Common.Network;

/// <summary>
/// The reason codes the server sends with a disconnect, which the client reads back to show why it was dropped.
/// </summary>
internal static class DisconnectReasons
{
    public const string JoinReplayAppliedTimeout = "JoinReplayAppliedTimeout";
    public const string JoinBaselineLimit = "JoinBaselineLimit";
    public const string JoinReplayQueueLimit = "JoinReplayQueueLimit";
    public const string JoinCampaignEntryTimeout = "JoinCampaignEntryTimeout";
    public const string ServerRestarting = "ServerRestarting";
    public const string BattleHostUnresponsive = "BattleHostUnresponsive";
}
