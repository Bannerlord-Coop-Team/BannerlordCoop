using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Actions.Messages;

public readonly struct HeroTeleported : IEvent
{
    public readonly Hero Hero;
    public readonly Settlement TargetSettlement;
    public readonly MobileParty TargetParty;
    public readonly TeleportHeroAction.TeleportationDetail Detail;

    public HeroTeleported(
        Hero hero,
        Settlement targetSettlement,
        MobileParty targetParty,
        TeleportHeroAction.TeleportationDetail detail)
    {
        Hero = hero;
        TargetSettlement = targetSettlement;
        TargetParty = targetParty;
        Detail = detail;
    }
}