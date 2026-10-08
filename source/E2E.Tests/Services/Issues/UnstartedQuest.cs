using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace E2E.Tests.Services.Issues;

internal class UnstartedQuest : QuestBase
{
    public UnstartedQuest(string questId, Hero questGiver)
        : base(questId, questGiver, CampaignTime.Never, 0)
    {
    }

    public override TextObject Title => new TextObject("Unstarted quest");

    public override bool IsRemainingTimeHidden => true;

    public override void SetDialogs()
    {
    }

    public override void InitializeQuestOnGameLoad()
    {
    }
}
