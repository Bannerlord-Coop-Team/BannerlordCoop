using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;

/// <summary>
/// Event that is handled on server side when Kingdom.RemoveDecision method is called.
/// </summary>
public readonly struct DecisionRemoved : IEvent
{
    public readonly Kingdom Kingdom;
    public readonly int Index;

    public DecisionRemoved(Kingdom kingdom, int index)
    {
        Kingdom = kingdom;
        Index = index;
    }
}