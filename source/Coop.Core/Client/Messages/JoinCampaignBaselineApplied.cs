using Common.Messaging;

namespace Coop.Core.Client.Messages;

/// <summary>
/// Reports whether the latest join baseline was applied in full.
/// </summary>
public sealed class JoinCampaignBaselineApplied : IEvent
{
    public bool Success { get; }

    public JoinCampaignBaselineApplied(bool success)
    {
        Success = success;
    }
}
