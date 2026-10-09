using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IFinalizationProofStrategy
{
    // Server: run the vanilla outcome branch the proof value stands for
    bool TryRunBranch(QuestBase quest, byte proof);
}
