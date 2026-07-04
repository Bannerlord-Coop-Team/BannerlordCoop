using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyTroopRecruited : IEvent
{
    public readonly Hero RecruiterHero;
    public readonly Settlement Settlement;
    public readonly Hero TroopSource;
    public readonly CharacterObject Troop;
    public readonly int Amount;

    public NotifyTroopRecruited(Hero recruiterHero, Settlement settlement, Hero troopSource, CharacterObject troop, int amount)
    {
        RecruiterHero = recruiterHero;
        Settlement = settlement;
        TroopSource = troopSource;
        Troop = troop;
        Amount = amount;
    }
}
