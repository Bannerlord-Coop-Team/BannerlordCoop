using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

/// <summary>
/// Event raised on the server when Kingdom.AddPolicy / RemovePolicy is called.
/// </summary>
public readonly struct KingdomPolicyChanged : IEvent
{
    public readonly Kingdom Kingdom;
    public readonly PolicyObject Policy;
    public readonly bool IsAdd;

    public KingdomPolicyChanged(Kingdom kingdom, PolicyObject policy, bool isAdd)
    {
        Kingdom = kingdom;
        Policy = policy;
        IsAdd = isAdd;
    }
}
