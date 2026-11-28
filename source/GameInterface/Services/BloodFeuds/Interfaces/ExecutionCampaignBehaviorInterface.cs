using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.CoopSessionData;
using GameInterface.Services.BloodFeuds.Data;
using GameInterface.Services.BloodFeuds.Messages;
using GameInterface.Services.Heroes;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Helpers;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Actions.ChangeBloodFeudStateAction;

namespace GameInterface.Services.BloodFeuds.Interfaces;

/// <summary>
/// Server side <see cref="ExecutionCampaignBehavior"/> with its blood feud state kept per player clan in <see cref="BloodFeudPlayerData"/>.
/// The server's clan blood feud fields stay unset, each client mirrors only its own clan's feuds into them.
/// </summary>
public interface IExecutionCampaignBehaviorInterface : IGameAbstraction
{
    void OnPrisonerDonatedToSettlement(MobileParty oldOwner, FlattenedTroopRoster prisonerRoster);
    void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail);
    void OnHeroRelationChanged(Hero effectiveHero, Hero effectiveHeroGainedRelationWith);
    void OnHeroTakenPrisoner(PartyBase capturer, Hero prisoner);
    void OnHeroChangedClan(Hero hero);
    void UpdateAllPlayerClanMemberExecutionStates();
    void DailyTickHero(Hero hero);
    void QuarterHourlyTick();
    void CanHeroBeReleased(Hero hero, ref bool result);
    void SettleBloodFeudByRansomPayment(Hero playerHero, Clan clan);
    void RequestSettleBloodFeudByRansomPayment(Clan clan);
    void SettleBloodFeudByAgreement(Clan playerClan, Clan otherPlayerClan);
    bool HasBloodFeud(Clan playerClan, Clan clan);
    bool HasBloodFeudWithAnyPlayer(Clan clan);
    void ApplyBloodFeuds(Dictionary<string, BloodFeudData> feuds);
    void ShowSettlementExecutionNotice(Hero clanMember, Clan clanWithFeud, Settlement settlement, CampaignTime executionDate);
}

internal class ExecutionCampaignBehaviorInterface : IExecutionCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<ExecutionCampaignBehaviorInterface>();

    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INetwork network;

    private BloodFeudPlayerData Data => coopSessionProvider.CoopSession.BloodFeudPlayerData;

    public ExecutionCampaignBehaviorInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        INetwork network)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.network = network;
    }

    public void OnPrisonerDonatedToSettlement(MobileParty oldOwner, FlattenedTroopRoster prisonerRoster)
    {
        Clan feudedClan = oldOwner.ActualClan;
        // Players decide what happens to the captives their clan holds
        if (TryGetPlayerClan(feudedClan, out _)) return;

        foreach (var playerClan in GetPlayerClans())
        {
            if (!HasBloodFeud(playerClan.ClanId, feudedClan)) continue;

            foreach (CharacterObject troop in prisonerRoster.Troops)
            {
                Hero prisoner = troop.HeroObject;
                if (!troop.IsHero || prisoner.IsPlayerHero() || prisoner.Clan != playerClan.Clan) continue;
                if (!HeroExecutionRules.IsExecutable(prisoner, out _)) continue;
                if (TryGetPendingSettlementExecution(playerClan.ClanId, prisoner, out _)) continue;

                StartClanMemberExecutionAtSettlement(playerClan, prisoner, feudedClan);
            }
        }
    }

    public void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail)
    {
        if (detail == KillCharacterAction.KillCharacterActionDetail.Executed && victim.Clan != null && killer?.Clan != null)
        {
            // The executing clan goes first, so a feud between two player clans starts from the executing side like vanilla
            var playerClans = GetPlayerClans().OrderByDescending(playerClan => playerClan.Clan == killer.Clan).ToList();
            foreach (var playerClan in playerClans)
            {
                if (killer.Clan == playerClan.Clan && killer.IsPlayerHero())
                {
                    OnPlayerExecutedHero(playerClan, victim);
                }
                else if (victim.Clan == playerClan.Clan)
                {
                    OnPlayerClanMemberExecuted(playerClan, victim, killer);
                }
                else if (killer.Clan == playerClan.Clan && HasBloodFeud(playerClan.ClanId, victim.Clan))
                {
                    OnPlayerMemberExecutedAHero(playerClan, victim, killer);
                }
            }

            // Counted in a second pass, so a feud the first pass started on both player clans already exists
            foreach (var playerClan in playerClans)
            {
                if (killer.Clan == playerClan.Clan && TryGetBloodFeud(playerClan.ClanId, victim.Clan, out var victimClanFeud) && victimClanFeud.HasBloodFeud)
                {
                    victimClanFeud.ExecutionsReceivedCount++;
                    SendBloodFeuds(playerClan);
                }
                else if (victim.Clan == playerClan.Clan && TryGetBloodFeud(playerClan.ClanId, killer.Clan, out var killerClanFeud) && killerClanFeud.HasBloodFeud)
                {
                    killerClanFeud.ExecutionsDoneCount++;
                    SendBloodFeuds(playerClan);
                }
            }
        }

        if (!objectManager.TryGetId(victim, out var victimId)) return;

        foreach (var pendingExecutions in Data.PlayerClanHeroesPendingMapEventEndExecution.Values)
            pendingExecutions.Remove(victimId);

        foreach (var pendingExecutions in Data.PlayerClanPendingSettlementExecutions.Values)
            pendingExecutions.Remove(victimId);
    }

    public void OnHeroRelationChanged(Hero effectiveHero, Hero effectiveHeroGainedRelationWith)
    {
        Clan effectiveClan = effectiveHero.Clan;
        Clan gainedRelationWithClan = effectiveHeroGainedRelationWith.Clan;
        if (effectiveClan == null || gainedRelationWithClan == null) return;
        if (effectiveClan.GetRelationWithClan(gainedRelationWithClan) < ExecutionCampaignBehavior.BloodFeudEndRelationLevel) return;

        foreach (var playerClan in GetPlayerClans())
        {
            if (effectiveClan != playerClan.Clan && gainedRelationWithClan != playerClan.Clan) continue;

            if (HasBloodFeud(playerClan.ClanId, effectiveClan))
            {
                ChangeBloodFeudState(playerClan, effectiveClan, null, ChangeBloodFeudActionDetail.SettledByRelationIncrease);
            }
            else if (HasBloodFeud(playerClan.ClanId, gainedRelationWithClan))
            {
                ChangeBloodFeudState(playerClan, gainedRelationWithClan, null, ChangeBloodFeudActionDetail.SettledByRelationIncrease);
            }
        }
    }

    public void OnHeroTakenPrisoner(PartyBase capturer, Hero prisoner)
    {
        Hero capturerLeader = capturer?.LeaderHero;
        if (capturerLeader == null || IsHeldByPlayer(capturer)) return;

        // PlayerCaptivityServerHandler rolls a captured player's execution
        if (prisoner.IsPlayerHero()) return;
        if (!HeroExecutionRules.IsExecutable(prisoner, out _)) return;

        foreach (var playerClan in GetPlayerClans())
        {
            if (capturerLeader.Clan == playerClan.Clan)
            {
                if (HasBloodFeud(playerClan.ClanId, prisoner.Clan))
                    AddPendingMapEventEndExecution(playerClan.ClanId, prisoner);
            }
            else if (prisoner.Clan == playerClan.Clan && HasBloodFeud(playerClan.ClanId, capturerLeader.Clan))
            {
                if (!objectManager.TryGetIdWithLogging(prisoner, out var prisonerId)) continue;
                if (!objectManager.TryGetIdWithLogging(capturerLeader.Clan, out var feudedClanId)) continue;

                SendToClan(playerClan, new NetworkNotifyBloodFeudClanMemberCaptured(prisonerId, feudedClanId));
            }
        }
    }

    public void OnHeroChangedClan(Hero hero)
    {
        MovePendingExecutions(hero);

        if (!playerManager.TryGetPlayer(hero, out var player)) return;

        Dictionary<string, BloodFeudData> feuds = null;
        Dictionary<string, PendingSettlementExecution> pendingExecutions = null;
        if (hero.Clan != null && objectManager.TryGetIdWithLogging(hero.Clan, out var clanId))
        {
            Data.PlayerClanBloodFeuds.TryGetValue(clanId, out feuds);
            Data.PlayerClanPendingSettlementExecutions.TryGetValue(clanId, out pendingExecutions);
        }

        SendToPlayer(player, new NetworkUpdateBloodFeuds(feuds));
        if (pendingExecutions == null) return;

        foreach (var pendingExecution in pendingExecutions)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(pendingExecution.Key, out var clanMember)) continue;

            Settlement settlement = clanMember.PartyBelongedToAsPrisoner?.Settlement;
            if (settlement == null || !objectManager.TryGetIdWithLogging(settlement, out var settlementId)) continue;

            SendToPlayer(player, new NetworkNotifyBloodFeudSettlementExecutionStarted(
                pendingExecution.Key, pendingExecution.Value.CapturerClanId, settlementId, pendingExecution.Value.ExecutionDateTicks));
        }
    }

    // Pending executions are kept under a player clan, so a hero who changes clan takes them along or they end
    private void MovePendingExecutions(Hero hero)
    {
        if (!objectManager.TryGetId(hero, out var heroId)) return;
        TryGetPlayerClan(hero.Clan, out var newPlayerClan);

        PendingSettlementExecution movedExecution = null;
        foreach (var pendingExecutions in Data.PlayerClanPendingSettlementExecutions)
        {
            if (pendingExecutions.Key == newPlayerClan?.ClanId) continue;
            if (!pendingExecutions.Value.TryGetValue(heroId, out var pendingExecution)) continue;

            pendingExecutions.Value.Remove(heroId);
            movedExecution = pendingExecution;

            var oldPlayerClan = GetPlayerClans().FirstOrDefault(candidate => candidate.ClanId == pendingExecutions.Key);
            if (oldPlayerClan != null)
                SendToClan(oldPlayerClan, new NetworkNotifyBloodFeudSettlementExecutionCancelled(heroId, pendingExecution.CapturerClanId));
        }

        if (newPlayerClan != null && !hero.IsPlayerHero())
        {
            if (movedExecution != null && hero.PartyBelongedToAsPrisoner?.Settlement != null &&
                objectManager.TryGetObject<Clan>(movedExecution.CapturerClanId, out var capturerClan) &&
                HasBloodFeud(newPlayerClan.ClanId, capturerClan))
            {
                StartClanMemberExecutionAtSettlement(newPlayerClan, hero, capturerClan, new CampaignTime(movedExecution.ExecutionDateTicks));
            }
            else
            {
                UpdateClanMemberExecutionState(newPlayerClan, hero);
            }
        }

        // After battle executions are kept under the captor's clan, so a party leader takes their prisoners' along
        MobileParty party = hero.PartyBelongedTo;
        if (party?.LeaderHero != hero) return;

        foreach (var prisonerElement in party.PrisonRoster.GetTroopRoster())
        {
            Hero prisoner = prisonerElement.Character.HeroObject;
            if (prisoner == null || !objectManager.TryGetId(prisoner, out var prisonerId)) continue;

            bool wasPending = false;
            foreach (var pendingExecutions in Data.PlayerClanHeroesPendingMapEventEndExecution)
            {
                if (pendingExecutions.Key == newPlayerClan?.ClanId) continue;

                wasPending |= pendingExecutions.Value.Remove(prisonerId);
            }

            if (wasPending && newPlayerClan != null && HasBloodFeud(newPlayerClan.ClanId, prisoner.Clan))
                AddPendingMapEventEndExecution(newPlayerClan.ClanId, prisoner);
        }
    }

    public void UpdateAllPlayerClanMemberExecutionStates()
    {
        foreach (var playerClan in GetPlayerClans())
        {
            UpdateAllPlayerClanMemberExecutionStates(playerClan);
        }
    }

    public void DailyTickHero(Hero hero)
    {
        if (!HeroExecutionRules.IsExecutable(hero, out _)) return;
        if (!objectManager.TryGetId(hero, out var heroId)) return;

        foreach (var pendingExecutions in Data.PlayerClanPendingSettlementExecutions.Values)
        {
            if (!pendingExecutions.TryGetValue(heroId, out var pendingExecution)) continue;

            if (new CampaignTime(pendingExecution.ExecutionDateTicks).IsPast &&
                objectManager.TryGetObjectWithLogging<Clan>(pendingExecution.CapturerClanId, out var capturerClan))
            {
                KillCharacterAction.ApplyByExecution(hero, capturerClan.Leader);
            }
            return;
        }

        if (hero.IsPlayerHero() || hero.PartyBelongedToAsPrisoner == null) return;

        if (!TryGetPlayerClan(hero.Clan, out var playerClan)) return;

        PartyBase captorParty = hero.PartyBelongedToAsPrisoner;
        if (IsHeldByPlayer(captorParty)) return;

        Hero executor = captorParty.IsMobile ? captorParty.LeaderHero : captorParty.Settlement.OwnerClan.Leader;
        if (executor == null) return;

        if (MBRandom.RandomFloat <= CalculatePlayerClanMemberExecutionProbability(playerClan, hero, executor))
        {
            KillCharacterAction.ApplyByExecution(hero, executor);
        }
    }

    public void QuarterHourlyTick()
    {
        foreach (var playerClan in GetPlayerClans())
        {
            if (!Data.PlayerClanHeroesPendingMapEventEndExecution.TryGetValue(playerClan.ClanId, out var pendingExecutions)) continue;

            foreach (var heroId in pendingExecutions.ToArray())
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(heroId, out var hero))
                {
                    pendingExecutions.Remove(heroId);
                    continue;
                }
                if (!HeroExecutionRules.IsExecutable(hero, out _)) continue;

                PartyBase captorParty = hero.PartyBelongedToAsPrisoner;
                Hero captor = captorParty?.LeaderHero;
                if (!hero.IsPrisoner || captor == null || IsHeldByPlayer(captorParty) || captorParty.MapEvent != null || captor.Clan != playerClan.Clan) continue;
                if (MBRandom.RandomFloat > 0.125f) continue;

                KillCharacterAction.ApplyByExecution(hero, captor);
                return;
            }
        }
    }

    public void CanHeroBeReleased(Hero hero, ref bool result)
    {
        // Holding a captive the host's config never lets anyone execute would keep them captive for good
        if (!HeroExecutionRules.IsExecutable(hero, out _)) return;
        if (!objectManager.TryGetId(hero, out var heroId)) return;

        PartyBase captorParty = hero.PartyBelongedToAsPrisoner;
        if (IsHeldByPlayer(captorParty)) return;

        if (Data.PlayerClanPendingSettlementExecutions.Values.Any(pendingExecutions => pendingExecutions.ContainsKey(heroId)) ||
            Data.PlayerClanHeroesPendingMapEventEndExecution.Values.Any(pendingExecutions => pendingExecutions.Contains(heroId)))
        {
            result = false;
            return;
        }

        if (!hero.IsPrisoner || captorParty?.IsMobile != true || captorParty.LeaderHero == null) return;

        Clan captorClan = captorParty.MobileParty.ActualClan;
        if (TryGetPlayerClan(hero.Clan, out var playerClan) && HasBloodFeud(playerClan.ClanId, captorClan))
        {
            result = false;
        }
    }

    public void SettleBloodFeudByRansomPayment(Hero playerHero, Clan clan)
    {
        // Ending a feud is the clan's decision, vanilla's player always leads their clan
        if (!playerHero.IsClanLeader) return;
        if (!TryGetPlayerClan(playerHero.Clan, out var playerClan)) return;
        // Player clans end their feuds with each other by agreement in a trade
        if (TryGetPlayerClan(clan, out _)) return;
        if (!TryGetBloodFeud(playerClan.ClanId, clan, out var feud) || !feud.HasBloodFeud) return;

        // The client priced it from its last counts, which can be behind the server's
        int bloodMoney = GetBloodMoneyForPlayerToPayAgainstClan(feud);
        if (playerHero.Gold < bloodMoney)
        {
            Logger.Warning("{Hero} can't pay the {BloodMoney} blood money to end the feud with {Clan}, they have {Gold}", playerHero.Name, bloodMoney, clan.Name, playerHero.Gold);
            return;
        }

        GiveGoldAction.ApplyBetweenCharacters(playerHero, null, bloodMoney);
        ChangeBloodFeudState(playerClan, clan, null, ChangeBloodFeudActionDetail.SettledByRansomPayment);
        ChangeRelationAction.SetRelationBetweenHeroes(playerClan.Clan.Leader, clan.Leader, -49);
    }

    public void SettleBloodFeudByAgreement(Clan playerClan, Clan otherPlayerClan)
    {
        if (!TryGetPlayerClan(playerClan, out var feudingPlayerClan)) return;
        if (!HasBloodFeud(feudingPlayerClan.ClanId, otherPlayerClan)) return;

        // Vanilla has no agreement detail, and the ransom one also clears the execution counts
        ChangeBloodFeudState(feudingPlayerClan, otherPlayerClan, null, ChangeBloodFeudActionDetail.SettledByRansomPayment, settledByAgreement: true);
    }

    public void RequestSettleBloodFeudByRansomPayment(Clan clan)
    {
        if (!objectManager.TryGetIdWithLogging(clan, out var clanId)) return;

        network.SendAll(new NetworkSettleBloodFeudByRansomPayment(clanId));
    }

    public bool HasBloodFeud(Clan playerClan, Clan clan)
    {
        return objectManager.TryGetId(playerClan, out var playerClanId) && HasBloodFeud(playerClanId, clan);
    }

    public bool HasBloodFeudWithAnyPlayer(Clan clan)
    {
        return GetPlayerClans().Any(playerClan => HasBloodFeud(playerClan.ClanId, clan));
    }

    public void ApplyBloodFeuds(Dictionary<string, BloodFeudData> feuds)
    {
        foreach (Clan clan in Clan.All)
        {
            BloodFeudData feud = null;
            if (feuds != null && objectManager.TryGetId(clan, out var clanId))
                feuds.TryGetValue(clanId, out feud);

            clan.HasBloodFeudWithPlayer = feud?.HasBloodFeud ?? false;
            clan.BloodFeudExecutionsDoneCount = feud?.ExecutionsDoneCount ?? 0;
            clan.BloodFeudExecutionsReceivedCount = feud?.ExecutionsReceivedCount ?? 0;
        }
    }

    public void ShowSettlementExecutionNotice(Hero clanMember, Clan clanWithFeud, Settlement settlement, CampaignTime executionDate)
    {
        TextObject textObject = new TextObject("{=8IP5qoyq}{CLAN_MEMBER.NAME}, your {RELATION}, is held prisoner by the {OTHER_CLAN} in {SETTLEMENT}. As your two clans have a blood feud, they plan to execute {?CLAN_MEMBER.GENDER}her{?}him{\\?} in {DAYS} {?DAYS > 1}days{?}day{\\?}.");
        StringHelpers.SetCharacterProperties("CLAN_MEMBER", clanMember.CharacterObject, textObject);
        textObject.SetTextVariable("SETTLEMENT", settlement.Name);
        textObject.SetTextVariable("DAYS", executionDate.RemainingDaysFromNow);
        textObject.SetTextVariable("OTHER_CLAN", clanWithFeud.Name);
        textObject.SetTextVariable("RELATION", ConversationHelper.GetHeroRelationToHeroTextShort(clanMember, Hero.MainHero, uppercaseFirst: false));
        Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new BloodFeudClanMemberCapturedMapNotification(clanMember, executionDate, settlement, textObject));
    }

    private void OnPlayerExecutedHero(BloodFeudPlayerClan playerClan, Hero victim)
    {
        // KillCharacterActionPatches charges the trait penalty, since it runs before this event
        if (!HasBloodFeud(playerClan.ClanId, victim.Clan))
        {
            ChangeBloodFeudState(playerClan, victim.Clan, victim, ChangeBloodFeudActionDetail.StartedByPlayerExecuteAHero);
        }
    }

    private void OnPlayerClanMemberExecuted(BloodFeudPlayerClan playerClan, Hero executedHero, Hero executor)
    {
        if (!HasBloodFeud(playerClan.ClanId, executor.Clan))
        {
            ChangeBloodFeudState(playerClan, executor.Clan, executedHero, ChangeBloodFeudActionDetail.StartedByAIExecutePlayerRelative);
        }

        if (executedHero.IsPlayerHero()) return;
        if (!objectManager.TryGetIdWithLogging(executedHero, out var executedHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(executor, out var executorId)) return;

        SendToClan(playerClan, new NetworkNotifyBloodFeudClanMemberExecuted(executedHeroId, executorId));
    }

    private void OnPlayerMemberExecutedAHero(BloodFeudPlayerClan playerClan, Hero heroToExecute, Hero clanMember)
    {
        if (!objectManager.TryGetIdWithLogging(heroToExecute, out var executedHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(clanMember, out var clanMemberId)) return;

        SendToClan(playerClan, new NetworkNotifyBloodFeudLordExecuted(executedHeroId, clanMemberId));
    }

    // ChangeBloodFeudStateAction.ApplyInternal for one player clan
    private void ChangeBloodFeudState(BloodFeudPlayerClan playerClan, Clan clanWithFeud, Hero executedHero, ChangeBloodFeudActionDetail detail, bool settledByAgreement = false)
    {
        if (!objectManager.TryGetIdWithLogging(clanWithFeud, out var clanWithFeudId)) return;

        bool hasBloodFeud = IsBloodFeudStart(detail);

        int penalizedClanCount = 0;
        if (hasBloodFeud)
        {
            Hero playerClanLeader = playerClan.Clan.Leader;
            ChangeRelationAction.SetRelationBetweenHeroes(playerClanLeader, clanWithFeud.Leader, Campaign.Current.Models.DiplomacyModel.MinRelationLimit);
            foreach (Clan clan in Clan.All)
            {
                if (clan == playerClan.Clan) continue;

                int relationPenalty = ExecutionCampaignBehavior.GetBloodFeudStartRelationPenaltyToOtherClan(executedHero, clan);
                if (relationPenalty == 0) continue;

                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(playerClanLeader, clan.Leader, relationPenalty, showQuickNotification: false);
                penalizedClanCount++;
            }
        }

        ApplyBloodFeudState(playerClan, clanWithFeudId, executedHero, detail, penalizedClanCount, settledByAgreement);

        // A feud between two player clans is one feud, so the other clan starts or ends it too
        if (!TryGetPlayerClan(clanWithFeud, out var otherPlayerClan)) return;
        if (HasBloodFeud(otherPlayerClan.ClanId, playerClan.Clan) == hasBloodFeud) return;

        ApplyBloodFeudState(otherPlayerClan, playerClan.ClanId, executedHero, GetOtherSideDetail(detail), penalizedClanCount: 0, settledByAgreement);
    }

    // ChangeBloodFeudStateAction's clan fields and OnBloodFeudStateChanged for one player clan
    private void ApplyBloodFeudState(BloodFeudPlayerClan playerClan, string clanId, Hero executedHero, ChangeBloodFeudActionDetail detail, int penalizedClanCount, bool settledByAgreement)
    {
        BloodFeudData feud = GetOrAddBloodFeud(playerClan.ClanId, clanId);
        feud.HasBloodFeud = IsBloodFeudStart(detail);
        if (detail == ChangeBloodFeudActionDetail.SettledByRansomPayment)
        {
            feud.ExecutionsDoneCount = 0;
            feud.ExecutionsReceivedCount = 0;
        }
        SendBloodFeuds(playerClan);

        objectManager.TryGetId(executedHero, out var executedHeroId);
        SendToClan(playerClan, new NetworkNotifyBloodFeudStateChanged(clanId, executedHeroId, detail, penalizedClanCount, settledByAgreement));

        UpdateAllPlayerClanMemberExecutionStates(playerClan);
    }

    private static bool IsBloodFeudStart(ChangeBloodFeudActionDetail detail)
    {
        return detail == ChangeBloodFeudActionDetail.StartedByPlayerExecuteAHero || detail == ChangeBloodFeudActionDetail.StartedByAIExecutePlayerRelative;
    }

    private static ChangeBloodFeudActionDetail GetOtherSideDetail(ChangeBloodFeudActionDetail detail)
    {
        return detail switch
        {
            ChangeBloodFeudActionDetail.StartedByPlayerExecuteAHero => ChangeBloodFeudActionDetail.StartedByAIExecutePlayerRelative,
            ChangeBloodFeudActionDetail.StartedByAIExecutePlayerRelative => ChangeBloodFeudActionDetail.StartedByPlayerExecuteAHero,
            _ => detail,
        };
    }

    private void UpdateAllPlayerClanMemberExecutionStates(BloodFeudPlayerClan playerClan)
    {
        bool hasBloodFeud = Data.PlayerClanBloodFeuds.TryGetValue(playerClan.ClanId, out var feuds) && feuds.Values.Any(feud => feud.HasBloodFeud);
        bool hasPendingExecution = Data.PlayerClanPendingSettlementExecutions.TryGetValue(playerClan.ClanId, out var pendingExecutions) && pendingExecutions.Count > 0;
        if (!hasBloodFeud && !hasPendingExecution) return;

        foreach (Hero hero in playerClan.Clan.Heroes.ToList())
        {
            if (!hero.IsPlayerHero())
            {
                UpdateClanMemberExecutionState(playerClan, hero);
            }
        }
    }

    private void UpdateClanMemberExecutionState(BloodFeudPlayerClan playerClan, Hero clanMember)
    {
        Clan pendingClan = null;
        if (TryGetPendingSettlementExecution(playerClan.ClanId, clanMember, out var pendingExecution))
        {
            objectManager.TryGetObjectWithLogging(pendingExecution.CapturerClanId, out pendingClan);
        }

        if (HeroExecutionRules.IsExecutable(clanMember, out _) &&
            GetClanMemberCurrentExecutionData(playerClan, clanMember, pendingClan, out var currentExecutorClan))
        {
            if (pendingClan == null)
            {
                StartClanMemberExecutionAtSettlement(playerClan, clanMember, currentExecutorClan);
            }
            else if (pendingClan != currentExecutorClan)
            {
                CancelClanMemberExecutionAtSettlement(playerClan, clanMember, pendingClan);
                StartClanMemberExecutionAtSettlement(playerClan, clanMember, currentExecutorClan);
            }
        }
        else if (pendingClan != null)
        {
            CancelClanMemberExecutionAtSettlement(playerClan, clanMember, pendingClan);
        }
    }

    private bool GetClanMemberCurrentExecutionData(BloodFeudPlayerClan playerClan, Hero hero, Clan pendingClan, out Clan currentExecutorClan)
    {
        currentExecutorClan = null;
        if (IsHeldByPlayer(hero.PartyBelongedToAsPrisoner)) return false;

        Settlement prison = hero.PartyBelongedToAsPrisoner?.Settlement;

        if (pendingClan != null && HasBloodFeud(playerClan.ClanId, pendingClan) && prison?.OwnerClan.MapFaction == pendingClan.MapFaction && !pendingClan.IsEliminated)
        {
            currentExecutorClan = pendingClan;
            return true;
        }

        if (prison != null && HasBloodFeud(playerClan.ClanId, prison.OwnerClan))
        {
            currentExecutorClan = prison.OwnerClan;
            return true;
        }

        return false;
    }

    private void StartClanMemberExecutionAtSettlement(BloodFeudPlayerClan playerClan, Hero clanMember, Clan clanWithFeud, CampaignTime? executionDate = null)
    {
        Settlement settlement = clanMember.PartyBelongedToAsPrisoner.Settlement;
        if (!objectManager.TryGetIdWithLogging(clanMember, out var clanMemberId)) return;
        if (!objectManager.TryGetIdWithLogging(clanWithFeud, out var clanId)) return;
        if (!objectManager.TryGetIdWithLogging(settlement, out var settlementId)) return;

        long executionDateTicks = (executionDate ?? CampaignTime.DaysFromNow(ExecutionCampaignBehavior.DaysTillBloodFeudExecutionInSettlement)).NumTicks;
        if (!Data.PlayerClanPendingSettlementExecutions.TryGetValue(playerClan.ClanId, out var pendingExecutions))
        {
            pendingExecutions = new Dictionary<string, PendingSettlementExecution>();
            Data.PlayerClanPendingSettlementExecutions[playerClan.ClanId] = pendingExecutions;
        }
        pendingExecutions[clanMemberId] = new PendingSettlementExecution(clanId, executionDateTicks);

        SendToClan(playerClan, new NetworkNotifyBloodFeudSettlementExecutionStarted(clanMemberId, clanId, settlementId, executionDateTicks));
    }

    private void CancelClanMemberExecutionAtSettlement(BloodFeudPlayerClan playerClan, Hero clanMember, Clan capturerClan)
    {
        if (!objectManager.TryGetIdWithLogging(clanMember, out var clanMemberId)) return;
        if (!objectManager.TryGetIdWithLogging(capturerClan, out var clanId)) return;

        Data.PlayerClanPendingSettlementExecutions[playerClan.ClanId].Remove(clanMemberId);

        SendToClan(playerClan, new NetworkNotifyBloodFeudSettlementExecutionCancelled(clanMemberId, clanId));
    }

    private float CalculatePlayerClanMemberExecutionProbability(BloodFeudPlayerClan playerClan, Hero clanMember, Hero executor)
    {
        if (HasBloodFeud(playerClan.ClanId, executor.Clan))
        {
            float chance = MBMath.ClampFloat(clanMember.CaptivityStartTime.ElapsedDaysUntilNow * 0.1f, 0f, 1f);
            return chance * chance;
        }

        int relation = executor.Clan.GetRelationWithClan(clanMember.Clan);
        if (relation > ExecutionCampaignBehavior.PlayerExecutionRelationThreshold) return 0f;

        int mercy = executor.GetTraitLevel(DefaultTraits.Mercy);
        return MathF.Max(((-relation * 0.3f) - (mercy * 5f)) * 0.01f, 0f);
    }

    private static int GetBloodMoneyForPlayerToPayAgainstClan(BloodFeudData feud)
    {
        int bloodMoney = 5000;
        if (feud.ExecutionsDoneCount < feud.ExecutionsReceivedCount)
        {
            bloodMoney += 50000 * (feud.ExecutionsReceivedCount - feud.ExecutionsDoneCount);
        }
        return bloodMoney;
    }

    private void SendBloodFeuds(BloodFeudPlayerClan playerClan)
    {
        Data.PlayerClanBloodFeuds.TryGetValue(playerClan.ClanId, out var feuds);
        SendToClan(playerClan, new NetworkUpdateBloodFeuds(feuds));
    }

    private void SendToClan(BloodFeudPlayerClan playerClan, IMessage message)
    {
        foreach (var player in playerClan.Players)
        {
            SendToPlayer(player, message);
        }
    }

    private void SendToPlayer(Player player, IMessage message)
    {
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;

        network.Send(peer, message);
    }

    private void AddPendingMapEventEndExecution(string playerClanId, Hero hero)
    {
        if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) return;

        if (!Data.PlayerClanHeroesPendingMapEventEndExecution.TryGetValue(playerClanId, out var pendingExecutions))
        {
            pendingExecutions = new List<string>();
            Data.PlayerClanHeroesPendingMapEventEndExecution[playerClanId] = pendingExecutions;
        }
        if (!pendingExecutions.Contains(heroId))
            pendingExecutions.Add(heroId);
    }

    private bool TryGetPendingSettlementExecution(string playerClanId, Hero hero, out PendingSettlementExecution pendingExecution)
    {
        pendingExecution = null;
        return objectManager.TryGetId(hero, out var heroId) &&
            Data.PlayerClanPendingSettlementExecutions.TryGetValue(playerClanId, out var pendingExecutions) &&
            pendingExecutions.TryGetValue(heroId, out pendingExecution);
    }

    private bool HasBloodFeud(string playerClanId, Clan clan)
    {
        return TryGetBloodFeud(playerClanId, clan, out var feud) && feud.HasBloodFeud;
    }

    private bool TryGetBloodFeud(string playerClanId, Clan clan, out BloodFeudData feud)
    {
        feud = null;
        return clan != null &&
            objectManager.TryGetId(clan, out var clanId) &&
            Data.PlayerClanBloodFeuds.TryGetValue(playerClanId, out var feuds) &&
            feuds.TryGetValue(clanId, out feud);
    }

    private BloodFeudData GetOrAddBloodFeud(string playerClanId, string clanId)
    {
        if (!Data.PlayerClanBloodFeuds.TryGetValue(playerClanId, out var feuds))
        {
            feuds = new Dictionary<string, BloodFeudData>();
            Data.PlayerClanBloodFeuds[playerClanId] = feuds;
        }

        if (!feuds.TryGetValue(clanId, out var feud))
        {
            feud = new BloodFeudData();
            feuds[clanId] = feud;
        }
        return feud;
    }

    // Players decide what happens to the captives they or their clan's dungeons hold, vanilla only executes for AI captors
    private bool IsHeldByPlayer(PartyBase captorParty)
    {
        return captorParty?.LeaderHero?.IsPlayerHero() == true || TryGetPlayerClan(captorParty?.Settlement?.OwnerClan, out _);
    }

    private bool TryGetPlayerClan(Clan clan, out BloodFeudPlayerClan playerClan)
    {
        playerClan = clan == null ? null : GetPlayerClans().FirstOrDefault(candidate => candidate.Clan == clan);
        return playerClan != null;
    }

    private List<BloodFeudPlayerClan> GetPlayerClans()
    {
        var playerClans = new List<BloodFeudPlayerClan>();
        foreach (var player in playerManager.Players)
        {
            if (!objectManager.TryGetObject<Hero>(player.HeroId, out var hero) || hero.Clan == null) continue;

            var playerClan = playerClans.FirstOrDefault(candidate => candidate.Clan == hero.Clan);
            if (playerClan == null)
            {
                if (!objectManager.TryGetId(hero.Clan, out var clanId)) continue;

                playerClan = new BloodFeudPlayerClan(hero.Clan, clanId);
                playerClans.Add(playerClan);
            }
            playerClan.Players.Add(player);
        }
        return playerClans;
    }

    private sealed class BloodFeudPlayerClan
    {
        public Clan Clan { get; }
        public string ClanId { get; }
        public List<Player> Players { get; } = new();

        public BloodFeudPlayerClan(Clan clan, string clanId)
        {
            Clan = clan;
            ClanId = clanId;
        }
    }
}
