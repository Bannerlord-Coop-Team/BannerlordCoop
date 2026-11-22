using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>
/// Local event published on a client when its quest reaches one of its outcome branches, the proof
/// value tells the server which one.
/// </summary>
public readonly struct QuestBranchRequested : IEvent
{
    public readonly QuestBase Quest;
    public readonly byte Proof;

    public QuestBranchRequested(QuestBase quest, byte proof)
    {
        Quest = quest;
        Proof = proof;
    }
}
