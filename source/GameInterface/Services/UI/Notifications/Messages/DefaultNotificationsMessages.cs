using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyCompanionRemoved : IEvent
{
    public readonly Clan Clan;
    public readonly Hero Hero;
    public readonly RemoveCompanionAction.RemoveCompanionDetail Detail;

    public NotifyCompanionRemoved(
        Clan clan,
        Hero hero,
        RemoveCompanionAction.RemoveCompanionDetail detail)
    {
        Clan = clan;
        Hero = hero;
        Detail = detail;
    }
}

public readonly struct NotifyClanTierIncreased : IEvent
{
    public readonly Clan Clan;
    public readonly bool ShouldNotify;

    public NotifyClanTierIncreased(
        Clan clan,
        bool shouldNotify)
    {
        Clan = clan;
        ShouldNotify = shouldNotify;
    }
}

public readonly struct NotifyRelationChanged : IEvent
{
    public readonly Hero EffectiveHero;
    public readonly Hero EffectiveHeroGainedRelationWith;
    public readonly int RelationChange;
    public readonly bool ShowNotification;
    public readonly ChangeRelationAction.ChangeRelationDetail Detail;
    public readonly Hero OriginalHero;
    public readonly Hero OriginalGainedRelationWith;

    public NotifyRelationChanged(
        Hero effectiveHero,
        Hero effectiveHeroGainedRelationWith,
        int relationChange,
        bool showNotification,
        ChangeRelationAction.ChangeRelationDetail detail,
        Hero originalHero,
        Hero originalGainedRelationWith)
    {
        EffectiveHero = effectiveHero;
        EffectiveHeroGainedRelationWith = effectiveHeroGainedRelationWith;
        RelationChange = relationChange;
        ShowNotification = showNotification;
        Detail = detail;
        OriginalHero = originalHero;
        OriginalGainedRelationWith = originalGainedRelationWith;
    }
}

public readonly struct NotifyTroopsDeserted : IEvent
{
    public readonly MobileParty MobileParty;
    public readonly TroopRoster DesertedTroops;

    public NotifyTroopsDeserted(
        MobileParty mobileParty,
        TroopRoster desertedTroops)
    {
        MobileParty = mobileParty;
        DesertedTroops = desertedTroops;
    }
}

public readonly struct NotifyClanDestroyed : IEvent
{
    public readonly TextObject DestroyedClanName;

    public NotifyClanDestroyed(
        TextObject destroyedClanName)
    {
        DestroyedClanName = destroyedClanName;
    }
}

public readonly struct NotifyBuildingLevelChanged : IEvent
{
    public readonly Town Town;
    public readonly Building Building;
    public readonly int LevelChange;

    public NotifyBuildingLevelChanged(
        Town town,
        Building building,
        int levelChange)
    {
        Town = town;
        Building = building;
        LevelChange = levelChange;
    }
}

public readonly struct NotifyHeroTeleportation : IEvent
{
    public readonly Hero Hero;
    public readonly Settlement TargetSettlement;
    public readonly MobileParty TargetParty;
    public readonly TeleportHeroAction.TeleportationDetail Detail;

    public NotifyHeroTeleportation(
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