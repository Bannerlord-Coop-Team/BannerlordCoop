using Common.Messaging;
using TaleWorlds.CampaignSystem.BattleWreckages;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct BattleWreckageRemoved : IEvent
{
    public readonly BattleWreckage BattleWreckage;

    public BattleWreckageRemoved(BattleWreckage battleWreckage)
    {
        BattleWreckage = battleWreckage;
    }
}
