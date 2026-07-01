using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyCompanionRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string HeroId;
    
    [ProtoMember(3)]
    public readonly RemoveCompanionAction.RemoveCompanionDetail Detail;

    public NetworkNotifyCompanionRemoved(
        string clanId,
        string heroId,
        RemoveCompanionAction.RemoveCompanionDetail detail)
    {
        ClanId = clanId;
        HeroId = heroId;
        Detail = detail;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyClanTierIncreased : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly bool ShouldNotify;

    public NetworkNotifyClanTierIncreased(
        string clanId,
        bool shouldNotify)
    {
        ClanId = clanId;
        ShouldNotify = shouldNotify;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyRelationChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string EffectiveHeroId;

    [ProtoMember(2)]
    public readonly string EffectiveHeroGainedRelationWithId;

    [ProtoMember(3)]
    public readonly int RelationChange;

    [ProtoMember(4)]
    public readonly bool ShowNotification;

    [ProtoMember(5)]
    public readonly ChangeRelationAction.ChangeRelationDetail Detail;

    [ProtoMember(6)]
    public readonly string OriginalHeroId;

    [ProtoMember(7)]
    public readonly string OriginalGainedRelationWithId;

    public NetworkNotifyRelationChanged(
        string effectiveHeroId,
        string effectiveHeroGainedRelationWithId,
        int relationChange,
        bool showNotification,
        ChangeRelationAction.ChangeRelationDetail detail,
        string originalHeroId,
        string originalGainedRelationWithId)
    {
        EffectiveHeroId = effectiveHeroId;
        EffectiveHeroGainedRelationWithId = effectiveHeroGainedRelationWithId;
        RelationChange = relationChange;
        ShowNotification = showNotification;
        Detail = detail;
        OriginalHeroId = originalHeroId;
        OriginalGainedRelationWithId = originalGainedRelationWithId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyTroopsDeserted : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly TroopRosterData DesertedTroopsData;

    public NetworkNotifyTroopsDeserted(
        string mobilePartyId,
        TroopRosterData desertedTroopsData)
    {
        MobilePartyId = mobilePartyId;
        DesertedTroopsData = desertedTroopsData;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyClanDestroyed : ICommand
{
    [ProtoMember(1)]
    public readonly TextObject DestroyedClanName;

    public NetworkNotifyClanDestroyed(
        TextObject destroyedClanName)
    {
        DestroyedClanName = destroyedClanName;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyBuildingLevelChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string TownId;

    [ProtoMember(2)]
    public readonly string BuildingId;

    [ProtoMember(3)]
    public readonly int LevelChange;

    public NetworkNotifyBuildingLevelChanged(
        string townId,
        string buildingId,
        int levelChange)
    {
        TownId = townId;
        BuildingId = buildingId;
        LevelChange = levelChange;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyHeroTeleportation : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string TargetSettlementId;

    [ProtoMember(3)]
    public readonly string TargetPartyId;

    [ProtoMember(4)]
    public readonly TeleportHeroAction.TeleportationDetail Detail;

    public NetworkNotifyHeroTeleportation(
        string heroId,
        string targetSettlementId,
        string targetPartyId,
        TeleportHeroAction.TeleportationDetail detail)
    {
        HeroId = heroId;
        TargetSettlementId = targetSettlementId;
        TargetPartyId = targetPartyId;
        Detail = detail;
    }
}