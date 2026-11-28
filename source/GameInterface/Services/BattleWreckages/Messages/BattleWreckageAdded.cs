using Common.Messaging;
using TaleWorlds.CampaignSystem.BattleWreckages;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct BattleWreckageAdded : IEvent
{
    public readonly BattleWreckage BattleWreckage;

    public BattleWreckageAdded(BattleWreckage battleWreckage)
    {
        BattleWreckage = battleWreckage;
    }
}
