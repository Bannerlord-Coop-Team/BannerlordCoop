using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Messages;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;

using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

internal interface IGangLeaderWeaponsQuestActions
{
    bool Apply(Quest quest, Hero owner, MobileParty party, GangLeaderWeaponsAction action);
    bool CompleteBattle(Quest quest, Hero owner, MobileParty party, MapEvent battle);
    bool StartApprovedBattle(Quest quest);
}

internal sealed class GangLeaderWeaponsQuestActions : IGangLeaderWeaponsQuestActions
{
    public bool StartApprovedBattle(Quest quest)
    {
        if (ModInformation.IsServer || !quest.IsOngoing || !quest._highCrimeRatingWillBeApplied ||
            quest._guardsParty == null || !quest._guardsParty.IsActive || quest._playerDodgedGuards) return false;
        PlayerEncounter.StartBattle();
        if (PlayerEncounter.Battle == null) return false;

        var town = quest.QuestGiver.CurrentSettlement;
        var upgradeLevel = town.IsTown ? town.Town.GetWallLevel() : 1;
        var troopCount = (int)(5f + (15f * quest._issueDifficulty));
        GameMenu.ActivateGameMenu("town");
        CampaignMission.OpenBattleMissionWhileEnteringSettlement(
            town.LocationComplex.GetLocationWithId("center").GetSceneName(upgradeLevel),
            upgradeLevel, troopCount, troopCount);
        quest._checkForBattleResult = true;
        return true;
    }

    public bool Apply(Quest quest, Hero owner, MobileParty party, GangLeaderWeaponsAction action)
    {
        if (ModInformation.IsClient) return false;
        if (quest == null || !quest.IsOngoing || owner == null || party == null) return false;

        using (new MainHeroSubstitutionScope(owner, party))
        using (new GangLeaderWeaponsActionScope(quest))
        using (new IssueFinalizeAuthorityGuard())
        {
            switch (action)
            {
                case GangLeaderWeaponsAction.CancelBattleStart:
                    if (party.MapEvent != null || quest._guardsParty?.MapEvent != null) return false;
                    quest._checkForBattleResult = false;
                    quest._highCrimeRatingWillBeApplied = false;
                    return true;
                case GangLeaderWeaponsAction.RefreshProgress:
                    quest.CalculateAndSetRequestedItemCountOnPlayer();
                    return true;
                case GangLeaderWeaponsAction.EnterTown:
                    if (party.CurrentSettlement != quest.QuestGiver.CurrentSettlement || party.Army != null ||
                        quest._playerDodgedGuards) return false;
                    quest.CalculateAndSetRequestedItemCountOnPlayer();
                    if (quest._collectedItemAmount < quest._requestedWeaponAmount / 3) return false;
                    if (quest._guardsParty == null) quest.CreateGuardsParty();
                    return true;
                case GangLeaderWeaponsAction.LeaveTown:
                    if (party.CurrentSettlement == quest.QuestGiver.CurrentSettlement) return false;
                    quest.OnSettlementLeft(party, quest.QuestGiver.CurrentSettlement);
                    return true;
                case GangLeaderWeaponsAction.DeliverWeapons:
                    if (party.CurrentSettlement != quest.QuestGiver.CurrentSettlement ||
                        !quest.CheckIfPlayerHasEnoughRequestedWeapons()) return false;
                    quest.PlayerSuccessfullyDeliveredWeapons();
                    return true;
                default:
                    if (!CanMeetGuards(quest, party)) return false;
                    return ApplyGuardChoice(quest, owner, action);
            }
        }
    }

    private static bool CanMeetGuards(Quest quest, MobileParty party)
    {
        return !quest._playerDodgedGuards && quest._guardsParty != null && quest._guardsParty.IsActive &&
            party.CurrentSettlement == quest.QuestGiver.CurrentSettlement && party.Army == null;
    }

    private static bool ApplyGuardChoice(Quest quest, Hero owner, GangLeaderWeaponsAction action)
    {
        switch (action)
        {
            case GangLeaderWeaponsAction.SurrenderWeapons:
                if (quest._weaponsThatGuardTook.Count != 0) return false;
                quest.DeleteAllWeaponsFromPlayer();
                return true;
            case GangLeaderWeaponsAction.Bribe:
                if (owner.Gold < quest._bribeGold) return false;
                quest.PlayerBribeGuard();
                return true;
            case GangLeaderWeaponsAction.Intimidate:
                if (owner.IsWounded || !quest.CheckPlayersPartySize()) return false;
                quest.PlayerDodgeGuardsLowCrimeRating();
                return true;
            case GangLeaderWeaponsAction.BeginPersuasion:
                if (quest._persuasionTriedOnce) return false;
                quest._persuasionTriedOnce = true;
                return true;
            case GangLeaderWeaponsAction.PersuasionSucceeded:
                if (!quest._persuasionTriedOnce) return false;
                quest.PlayerDodgedGuards();
                return true;
            case GangLeaderWeaponsAction.BeginBattle:
                if (owner.IsWounded || quest.CheckPlayersPartySize() || quest._checkForBattleResult) return false;
                quest._highCrimeRatingWillBeApplied = true;
                quest._checkForBattleResult = true;
                return true;
            default:
                return false;
        }
    }

    public bool CompleteBattle(Quest quest, Hero owner, MobileParty party, MapEvent battle)
    {
        if (ModInformation.IsClient) return false;
        if (quest == null || !quest.IsOngoing || !quest._checkForBattleResult || owner == null ||
            party == null || battle == null || quest._guardsParty == null ||
            !battle.InvolvedParties.Contains(quest._guardsParty.Party)) return false;
        if (battle.WinningSide != BattleSideEnum.Attacker && battle.WinningSide != BattleSideEnum.Defender) return false;

        var ownerSide = battle.AttackerSide.Parties.Any(p => p.Party == party.Party)
            ? BattleSideEnum.Attacker
            : battle.DefenderSide.Parties.Any(p => p.Party == party.Party)
                ? BattleSideEnum.Defender : BattleSideEnum.None;
        if (ownerSide == BattleSideEnum.None) return false;

        using (new MainHeroSubstitutionScope(owner, party))
        using (new GangLeaderWeaponsActionScope(quest))
        using (new IssueFinalizeAuthorityGuard())
        {
            quest._checkForBattleResult = false;
            if (battle.WinningSide == ownerSide)
            {
                quest.PlayerDodgedGuards();
            }
            else
            {
                quest.PlayerDefeatedAgainstGuards();
            }
        }
        return true;
    }
}

internal sealed class GangLeaderWeaponsActionScope : IDisposable
{
    [ThreadStatic] private static Quest current;
    private readonly Quest previous;

    public GangLeaderWeaponsActionScope(Quest quest)
    {
        previous = current;
        current = quest;
    }

    public static bool Contains(Quest quest) => current == quest;
    public static Quest Current => current;

    public void Dispose() => current = previous;
}
