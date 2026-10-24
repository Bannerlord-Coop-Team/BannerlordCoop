using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;

public readonly struct LandLordCompanyOfTroubleFoodStolen : IEvent
{
    public readonly QuestBase Quest;
    public readonly ItemObject Item;

    public LandLordCompanyOfTroubleFoodStolen(QuestBase quest, ItemObject item)
    {
        Quest = quest;
        Item = item;
    }
}
