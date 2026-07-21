using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyAllianceStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string Kingdom1Id;

    [ProtoMember(2)]
    public readonly string Kingdom2Id;

    public NetworkNotifyAllianceStarted(
        string kingdom1Id,
        string kingdom2Id)
    {
        Kingdom1Id = kingdom1Id;
        Kingdom2Id = kingdom2Id;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyAllianceEnded : ICommand
{
    [ProtoMember(1)]
    public readonly string Kingdom1Id;

    [ProtoMember(2)]
    public readonly string Kingdom2Id;

    public NetworkNotifyAllianceEnded(
        string kingdom1Id,
        string kingdom2Id)
    {
        Kingdom1Id = kingdom1Id;
        Kingdom2Id = kingdom2Id;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyCallWarToWarAgreementStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string CallingKingdomId;

    [ProtoMember(2)]
    public readonly string CalledKingdomId;

    [ProtoMember(3)]
    public readonly string KingdomToCallToWarAgainstId;

    public NetworkNotifyCallWarToWarAgreementStarted(
        string callingKingdomId,
        string calledKingdomId,
        string kingdomToCallToWarAgainstId)
    {
        CallingKingdomId = callingKingdomId;
        CalledKingdomId = calledKingdomId;
        KingdomToCallToWarAgainstId = kingdomToCallToWarAgainstId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyCallWarToWarAgreementEnded : ICommand
{
    [ProtoMember(1)]
    public readonly string CallingKingdomId;

    [ProtoMember(2)]
    public readonly string CalledKingdomId;

    [ProtoMember(3)]
    public readonly string KingdomToCallToWarAgainstId;

    public NetworkNotifyCallWarToWarAgreementEnded(
        string callingKingdomId,
        string calledKingdomId,
        string kingdomToCallToWarAgainstId)
    {
        CallingKingdomId = callingKingdomId;
        CalledKingdomId = calledKingdomId;
        KingdomToCallToWarAgainstId = kingdomToCallToWarAgainstId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifySettlementEntered : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly string SettlementId;

    [ProtoMember(3)]
    public readonly string HeroId;

    public NetworkNotifySettlementEntered(
        string mobilePartyId,
        string settlementId,
        string heroId)
    {
        MobilePartyId = mobilePartyId;
        SettlementId = settlementId;
        HeroId = heroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyPartyAddedToMapEvent : ICommand
{
    [ProtoMember(1)]
    public readonly string InvolvedPartyId;

    public NetworkNotifyPartyAddedToMapEvent(string involvedPartyId)
    {
        InvolvedPartyId = involvedPartyId;
    }
}

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
internal readonly struct NetworkNotifyRenownGained : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly int GainedRenown;

    [ProtoMember(3)]
    public readonly bool DoNotNotifyPlayer;

    public NetworkNotifyRenownGained(
        string heroId,
        int gainedRenown,
        bool doNotNotifyPlayer)
    {
        HeroId = heroId;
        GainedRenown = gainedRenown;
        DoNotNotifyPlayer = doNotNotifyPlayer;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyHideoutSpotted : ICommand
{
    [ProtoMember(1)]
    public readonly string SpottingPartyId;

    [ProtoMember(2)]
    public readonly string HideoutPartyId;

    public NetworkNotifyHideoutSpotted(
        string spottingPartyId,
        string hideoutPartyId)
    {
        SpottingPartyId = spottingPartyId;
        HideoutPartyId = hideoutPartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyHeroBecameFugitive : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly bool ShowNotification;

    public NetworkNotifyHeroBecameFugitive(
        string heroId,
        bool showNotification)
    {
        HeroId = heroId;
        ShowNotification = showNotification;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyPrisonerTaken : ICommand
{
    [ProtoMember(1)]
    public readonly string CapturerId;

    [ProtoMember(2)]
    public readonly string PrisonerId;

    public NetworkNotifyPrisonerTaken(
        string capturerId,
        string prisonerId)
    {
        CapturerId = capturerId;
        PrisonerId = prisonerId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyHeroPrisonerReleased : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string PartyId;

    [ProtoMember(3)]
    public readonly string FactionId;

    [ProtoMember(4)]
    public readonly EndCaptivityDetail Detail;

    [ProtoMember(5)]
    public readonly bool ShowNotification;

    public NetworkNotifyHeroPrisonerReleased(
        string heroId,
        string partyId,
        string factionId,
        EndCaptivityDetail detail,
        bool showNotification)
    {
        HeroId = heroId;
        PartyId = partyId;
        FactionId = factionId;
        Detail = detail;
        ShowNotification = showNotification;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyBattleStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string AttackerPartyId;

    [ProtoMember(2)]
    public readonly string DefenderPartyId;

    [ProtoMember(3)]
    public readonly string SettlementId;

    [ProtoMember(4)]
    public readonly bool ShowNotification;

    public NetworkNotifyBattleStarted(
        string attackerPartyId,
        string defenderPartyId,
        string settlementId,
        bool showNotification)
    {
        AttackerPartyId = attackerPartyId;
        DefenderPartyId = defenderPartyId;
        SettlementId = settlementId;
        ShowNotification = showNotification;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifySiegeEventStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string SiegeEventId;

    public NetworkNotifySiegeEventStarted(string siegeEventId)
    {
        SiegeEventId = siegeEventId;
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
internal readonly struct NetworkNotifyItemsLooted : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly ItemRosterElement[] ItemRosterData;

    public NetworkNotifyItemsLooted(
        string mobilePartyId,
        ItemRosterElement[] itemRosterData)
    {
        MobilePartyId = mobilePartyId;
        ItemRosterData = itemRosterData;
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
internal readonly struct NetworkNotifyHeroLevelledUp : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly bool ShouldNotify;

    public NetworkNotifyHeroLevelledUp(
        string heroId,
        bool shouldNotify)
    {
        HeroId = heroId;
        ShouldNotify = shouldNotify;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyHeroGainedSkill : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string SkillObjectId;

    [ProtoMember(3)]
    public readonly int Change;

    [ProtoMember(4)]
    public readonly bool ShouldNotify;

    public NetworkNotifyHeroGainedSkill(
        string heroId,
        string skillObjectId,
        int change,
        bool shouldNotify)
    {
        HeroId = heroId;
        SkillObjectId = skillObjectId;
        Change = change;
        ShouldNotify = shouldNotify;
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
internal readonly struct NetworkNotifyClanChangedFaction : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string OldKingdomId;

    [ProtoMember(3)]
    public readonly string NewKingdomId;

    [ProtoMember(4)]
    public readonly ChangeKingdomAction.ChangeKingdomActionDetail Detail;

    [ProtoMember(5)]
    public readonly bool ShowNotification;

    public NetworkNotifyClanChangedFaction(
        string clanId,
        string oldKingdomId,
        string newKingdomId,
        ChangeKingdomAction.ChangeKingdomActionDetail detail,
        bool showNotification)
    {
        ClanId = clanId;
        OldKingdomId = oldKingdomId;
        NewKingdomId = newKingdomId;
        Detail = detail;
        ShowNotification = showNotification;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyArmyCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string ArmyId;
    [ProtoMember(2)]
    public readonly string AiBehaviorObjectId;

    public NetworkNotifyArmyCreated(string armyId, string aiBehaviorObjectId)
    {
        ArmyId = armyId;
        AiBehaviorObjectId = aiBehaviorObjectId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifySiegeBombardmentHit : ICommand
{
    [ProtoMember(1)]
    public readonly string BesiegerPartyId;

    [ProtoMember(2)]
    public readonly string BesiegedSettlementId;

    [ProtoMember(3)]
    public readonly BattleSideEnum Side;

    [ProtoMember(4)]
    public readonly string WeaponId;

    [ProtoMember(5)]
    public readonly SiegeBombardTargets Target;

    public NetworkNotifySiegeBombardmentHit(
        string besiegerPartyId,
        string besiegedSettlementId,
        BattleSideEnum side,
        string weaponId,
        SiegeBombardTargets target)
    {
        BesiegerPartyId = besiegerPartyId;
        BesiegedSettlementId = besiegedSettlementId;
        Side = side;
        WeaponId = weaponId;
        Target = target;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifySiegeBombardmentWallHit : ICommand
{
    [ProtoMember(1)]
    public readonly string BesiegerPartyId;

    [ProtoMember(2)]
    public readonly string BesiegedSettlementId;

    [ProtoMember(3)]
    public readonly BattleSideEnum Side;

    [ProtoMember(4)]
    public readonly string WeaponId;

    [ProtoMember(5)]
    public readonly bool IsWallCracked;

    public NetworkNotifySiegeBombardmentWallHit(
        string besiegerPartyId,
        string besiegedSettlementId,
        BattleSideEnum side,
        string weaponId,
        bool isWallCracked)
    {
        BesiegerPartyId = besiegerPartyId;
        BesiegedSettlementId = besiegedSettlementId;
        Side = side;
        WeaponId = weaponId;
        IsWallCracked = isWallCracked;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifySiegeEngineDestroyed : ICommand
{
    [ProtoMember(1)]
    public readonly string BesiegerPartyId;

    [ProtoMember(2)]
    public readonly string BesiegedSettlementId;

    [ProtoMember(3)]
    public readonly BattleSideEnum Side;

    [ProtoMember(4)]
    public readonly string DestroyedEngineId;

    public NetworkNotifySiegeEngineDestroyed(
        string besiegerPartyId,
        string besiegedSettlementId,
        BattleSideEnum side,
        string destroyedEngineId)
    {
        BesiegerPartyId = besiegerPartyId;
        BesiegedSettlementId = besiegedSettlementId;
        Side = side;
        DestroyedEngineId = destroyedEngineId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyPartyJoinedArmy : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkNotifyPartyJoinedArmy(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyPartyAttachedAnotherParty : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkNotifyPartyAttachedAnotherParty(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyPartyRemovedFromArmy : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;
    [ProtoMember(2)]
    public readonly string ArmyId;

    public NetworkNotifyPartyRemovedFromArmy(string mobilePartyId, string armyId)
    {
        MobilePartyId = mobilePartyId;
        ArmyId = armyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkArmyDispersed : ICommand
{
    [ProtoMember(1)]
    public readonly string ArmyId;

    [ProtoMember(2)]
    public readonly Army.ArmyDispersionReason Reason;

    [ProtoMember(3)]
    public readonly bool IsPlayersArmy;

    public NetworkArmyDispersed(
        string armyId,
        Army.ArmyDispersionReason reason,
        bool isPlayersArmy)
    {
        ArmyId = armyId;
        Reason = reason;
        IsPlayersArmy = isPlayersArmy;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyHeroesMarried : ICommand
{
    [ProtoMember(1)]
    public readonly string FirstHeroId;

    [ProtoMember(2)]
    public readonly string SecondHeroId;

    [ProtoMember(3)]
    public readonly bool ShowNotification;

    public NetworkNotifyHeroesMarried(
        string firstHeroId,
        string secondHeroId,
        bool showNotification)
    {
        FirstHeroId = firstHeroId;
        SecondHeroId = secondHeroId;
        ShowNotification = showNotification;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyChildConceived : ICommand
{
    [ProtoMember(1)]
    public readonly string MotherId;

    public NetworkNotifyChildConceived(string motherId)
    {
        MotherId = motherId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyGivenBirth : ICommand
{
    [ProtoMember(1)]
    public readonly string MotherId;

    [ProtoMember(2)]
    public readonly List<string> AliveOffspringsIds;

    [ProtoMember(3)]
    public readonly int StillbornCount;

    public NetworkNotifyGivenBirth(
        string motherId,
        List<string> aliveOffspringsIds,
        int stillbornCount)
    {
        MotherId = motherId;
        AliveOffspringsIds = aliveOffspringsIds;
        StillbornCount = stillbornCount;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyHeroKilled : ICommand
{
    [ProtoMember(1)]
    public readonly string VictimHeroId;

    [ProtoMember(2)]
    public readonly string KillerId;

    [ProtoMember(3)]
    public readonly KillCharacterAction.KillCharacterActionDetail Detail;

    [ProtoMember(4)]
    public readonly bool ShowNotification;

    public NetworkNotifyHeroKilled(
        string victimHeroId,
        string killerId,
        KillCharacterAction.KillCharacterActionDetail detail,
        bool showNotification)
    {
        VictimHeroId = victimHeroId;
        KillerId = killerId;
        Detail = detail;
        ShowNotification = showNotification;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHeroSharedFoodWithAnotherHero : ICommand
{
    [ProtoMember(1)]
    public readonly string SupporterHeroId;

    [ProtoMember(2)]
    public readonly string SupportedHeroId;

    [ProtoMember(3)]
    public readonly float Influence;

    public NetworkHeroSharedFoodWithAnotherHero(
        string supporterHeroId,
        string supportedHeroId,
        float influence)
    {
        SupporterHeroId = supporterHeroId;
        SupportedHeroId = supportedHeroId;
        Influence = influence;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyClanDestroyed : ICommand
{
    [ProtoMember(1)]
    public readonly string DestroyedClanId;

    public NetworkNotifyClanDestroyed(
        string destroyedClanId)
    {
        DestroyedClanId = destroyedClanId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyHeroOrPartyGaveItem : ICommand
{
    [ProtoMember(1)]
    public readonly ValueTuple<string, string> GiverIds;

    [ProtoMember(2)]
    public readonly ValueTuple<string, string> ReceiverIds;

    [ProtoMember(3)]
    public readonly ItemRosterElement ItemRosterElement;

    [ProtoMember(4)]
    public readonly bool ShowNotification;

    public NetworkNotifyHeroOrPartyGaveItem(
        (string, string) giverIds,
        (string, string) receiverIds,
        ItemRosterElement itemRosterElement,
        bool showNotification)
    {
        GiverIds = giverIds;
        ReceiverIds = receiverIds;
        ItemRosterElement = itemRosterElement;
        ShowNotification = showNotification;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyRebellionFinished : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly string OldOwnerClanId;

    public NetworkNotifyRebellionFinished(
        string settlementId,
        string oldOwnerClanId)
    {
        SettlementId = settlementId;
        OldOwnerClanId = oldOwnerClanId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyTournamentFinished : ICommand
{
    [ProtoMember(1)]
    public readonly string WinnerId;

    [ProtoMember(2)]
    public readonly MBReadOnlyList<string> ParticipantsIds;

    [ProtoMember(3)]
    public readonly string TownId;

    [ProtoMember(4)]
    public readonly string PrizeId;

    public NetworkNotifyTournamentFinished(
        string winnerId,
        MBReadOnlyList<string> participantsIds,
        string townId,
        string prizeId)
    {
        WinnerId = winnerId;
        ParticipantsIds = participantsIds;
        TownId = townId;
        PrizeId = prizeId;
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