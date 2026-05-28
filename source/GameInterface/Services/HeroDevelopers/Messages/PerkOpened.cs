using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.HeroDevelopers.Messages;

public readonly struct PerkOpened : IEvent
{
    public readonly Hero Hero;
    public readonly PerkObject Perk;

    public PerkOpened(Hero hero, PerkObject perk)
    {
        Hero = hero;
        Perk = perk;
    }
}
