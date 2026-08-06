using System;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Gates;

public interface IGateDescriptor<TQuest>
{
    bool Prefix(TQuest instance);
}

public sealed record GateAndInjectDescriptor<TQuest>(
    Func<TQuest, Hero> QuestGiverSelector,
    Action<TQuest> PreBodyInjection,
    Func<TQuest, bool> InjectionCondition = null
) : IGateDescriptor<TQuest> where TQuest : QuestBase
{
    public bool Prefix(TQuest instance)
    {
        if (!IssueOwnershipRegistry.IsLocalPeerOwner(QuestGiverSelector(instance))) return false;
        // Must run before returning true, so its message enqueues before the vanilla body's own trailing side
        // effect (message ordering here depends on all messages being ReliableOrdered).
        if (InjectionCondition == null || InjectionCondition(instance)) PreBodyInjection(instance);
        return true;
    }
}
