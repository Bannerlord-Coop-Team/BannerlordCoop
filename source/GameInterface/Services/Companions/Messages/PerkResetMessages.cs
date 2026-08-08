using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Companions.Messages;

public readonly struct ResetPerksByArenaMaster : IEvent
{
    public readonly Hero MainHero;
    public readonly int PerkResetCost;
    public readonly Hero HeroForPerkReset;
    public readonly SkillObject SelectedSkillForReset;

    public ResetPerksByArenaMaster(
        Hero mainHero,
        int perkResetCost,
        Hero heroForPerkReset,
        SkillObject selectedSkillForReset)
    {
        MainHero = mainHero;
        PerkResetCost = perkResetCost;
        HeroForPerkReset = heroForPerkReset;
        SelectedSkillForReset = selectedSkillForReset;
    }
}

public readonly struct RemoveACompanionFromPlayerParty : IEvent
{
    public readonly Clan PlayerClan;

    public RemoveACompanionFromPlayerParty(Clan playerClan)
    {
        PlayerClan = playerClan;
    }
}