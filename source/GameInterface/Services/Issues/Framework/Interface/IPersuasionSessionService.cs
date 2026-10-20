using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

internal interface IPersuasionSessionService
{
    void Start(QuestBase quest, object target);

    bool TryConsume(QuestBase quest, object target);
}
