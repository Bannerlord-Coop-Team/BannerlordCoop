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
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Actions.ChangeBloodFeudStateAction;

namespace GameInterface.Services.BloodFeuds.Interfaces;

/// <summary>
/// Server side <see cref="ExecutionCampaignBehavior"/> with its blood feud state kept per player in <see cref="BloodFeudPlayerData"/>.
/// The server's clan blood feud fields stay unset, each client mirrors only its own player's feuds into them.
/// </summary>
public interface IExecutionCampaignBehaviorInterface : IGameAbstraction
{
    void OnPrisonerDonatedToSettlement(MobileParty oldOwner, FlattenedTroopRoster prisonerRoster);
    void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail);
    void OnHeroRelationChanged(Hero effectiveHero, Hero effectiveHeroGainedRelationWith);
    void OnHeroTakenPrisoner(PartyBase capturer, Hero prisoner);
    void UpdateAllPlayerClanMemberExecutionStates();
    void DailyTickHero(Hero hero);
    void QuarterHourlyTick();
    void CanHeroBeReleased(Hero hero, ref bool result);
    void SettleBloodFeudByRansomPayment(Hero playerHero, Clan clan);
    void RequestSettleBloodFeudByRansomPayment(Clan clan);
    bool HasBloodFeud(Hero playerHero, Clan clan);
    bool HasBloodFeudWithAnyPlayer(Clan clan);
    void SetPendingPlayerExecution(Hero playerHero, Hero executor);
    void ExecutePendingPlayers();
    void ApplyBloodFeuds(Dictionary<string, BloodFeudData> feuds);
}

internal class ExecutionCampaignBehaviorInterface : IExecutionCampaignBehaviorInterface
{
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INetwork network;

    // Dictionary<PlayerHeroId, ExecutorHeroId> for _isMainHeroExecuted, executed on the next CampaignTick
    private readonly Dictionary<string, string> pendingPlayerExecutions = new();

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
        foreach (var player in GetPlayers())
        {
            if (!HasBloodFeud(player.HeroId, feudedClan)) continue;

            foreach (CharacterObject troop in prisonerRoster.Troops)
            {
                Hero prisoner = troop.HeroObject;
                if (!troop.IsHero || prisoner.IsPlayerHero() || prisoner.Clan != player.Clan) continue;
                if (!HeroExecutionRules.IsExecutable(prisoner, out _)) continue;
                if (TryGetPendingSettlementExecution(player.HeroId, prisoner, out _)) continue;

                StartClanMemberExecutionAtSettlement(player, prisoner, feudedClan);
            }
        }
    }

    public void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail)
    {
        if (detail == KillCharacterAction.KillCharacterActionDetail.Executed && victim.Clan != null && killer?.Clan != null)
        {
            foreach (var player in GetPlayers())
            {
                if (killer == player.Hero)
                {
                    OnPlayerExecutedHero(player, victim);
                }
                else if (victim.Clan == player.Clan)
                {
                    OnPlayerClanMemberExecuted(player, victim, killer);
                }
                else if (killer.Clan == player.Clan && HasBloodFeud(player.HeroId, victim.Clan))
                {
                    OnPlayerMemberExecutedAHero(player, victim, killer);
                }

                if (TryGetBloodFeud(player.HeroId, victim.Clan, out var victimClanFeud) && victimClanFeud.HasBloodFeud)
                {
                    victimClanFeud.ExecutionsReceivedCount++;
                    SendBloodFeuds(player.Player);
                }
                else if (TryGetBloodFeud(player.HeroId, killer.Clan, out var killerClanFeud) && killerClanFeud.HasBloodFeud)
                {
                    killerClanFeud.ExecutionsDoneCount++;
                    SendBloodFeuds(player.Player);
                }
            }
        }

        if (!objectManager.TryGetId(victim, out var victimId)) return;

        foreach (var pendingExecutions in Data.PlayerHeroesPendingMapEventEndExecution.Values)
            pendingExecutions.Remove(victimId);

        foreach (var pendingExecutions in Data.PlayerPendingSettlementExecutions.Values)
            pendingExecutions.Remove(victimId);
    }

    public void OnHeroRelationChanged(Hero effectiveHero, Hero effectiveHeroGainedRelationWith)
    {
        Clan effectiveClan = effectiveHero.Clan;
        Clan gainedRelationWithClan = effectiveHeroGainedRelationWith.Clan;
        if (effectiveClan == null || gainedRelationWithClan == null) return;
        if (effectiveClan.GetRelationWithClan(gainedRelationWithClan) < ExecutionCampaignBehavior.BloodFeudEndRelationLevel) return;

        foreach (var player in GetPlayers())
        {
            if (effectiveClan != player.Clan && gainedRelationWithClan != player.Clan) continue;

            if (HasBloodFeud(player.HeroId, effectiveClan))
            {
                ChangeBloodFeudState(player, effectiveClan, null, ChangeBloodFeudActionDetail.SettledByRelationIncrease);
            }
            else if (HasBloodFeud(player.HeroId, gainedRelationWithClan))
            {
                ChangeBloodFeudState(player, gainedRelationWithClan, null, ChangeBloodFeudActionDetail.SettledByRelationIncrease);
            }
        }
    }

    public void OnHeroTakenPrisoner(PartyBase capturer, Hero prisoner)
    {
        Hero capturerLeader = capturer?.LeaderHero;
        if (capturerLeader == null) return;

        // PlayerCaptivityServerHandler rolls a captured player's execution
        if (prisoner.IsPlayerHero()) return;
        if (!HeroExecutionRules.IsExecutable(prisoner, out _)) return;

        foreach (var player in GetPlayers())
        {
            if (capturerLeader.Clan == player.Clan)
            {
                if (!capturerLeader.IsPlayerHero() && HasBloodFeud(player.HeroId, prisoner.Clan))
                    AddPendingMapEventEndExecution(player.HeroId, prisoner);
            }
            else if (prisoner.Clan == player.Clan && HasBloodFeud(player.HeroId, capturerLeader.Clan))
            {
                if (!objectManager.TryGetIdWithLogging(prisoner, out var prisonerId)) continue;
                if (!objectManager.TryGetIdWithLogging(capturerLeader.Clan, out var feudedClanId)) continue;

                SendToPlayer(player.Player, new NetworkNotifyBloodFeudClanMemberCaptured(prisonerId, feudedClanId));
            }
        }
    }

    public void UpdateAllPlayerClanMemberExecutionStates()
    {
        foreach (var player in GetPlayers())
        {
            UpdateAllPlayerClanMemberExecutionStates(player);
        }
    }

    public void DailyTickHero(Hero hero)
    {
        if (!HeroExecutionRules.IsExecutable(hero, out _)) return;
        if (!objectManager.TryGetId(hero, out var heroId)) return;

        foreach (var pendingExecutions in Data.PlayerPendingSettlementExecutions.Values)
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

        var clanPlayers = GetPlayers().Where(player => player.Clan == hero.Clan).ToList();
        if (clanPlayers.Count == 0) return;

        PartyBase captorParty = hero.PartyBelongedToAsPrisoner;
        Hero executor = captorParty.IsMobile ? captorParty.LeaderHero : captorParty.Settlement.OwnerClan.Leader;
        // Another player holding the captive decides for themselves, vanilla only rolls this for AI captors
        if (executor == null || executor.IsPlayerHero()) return;

        if (MBRandom.RandomFloat <= CalculatePlayerClanMemberExecutionProbability(clanPlayers, hero, executor))
        {
            KillCharacterAction.ApplyByExecution(hero, executor);
        }
    }

    public void QuarterHourlyTick()
    {
        foreach (var player in GetPlayers())
        {
            if (!Data.PlayerHeroesPendingMapEventEndExecution.TryGetValue(player.HeroId, out var pendingExecutions)) continue;

            foreach (var heroId in pendingExecutions.ToArray())
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(heroId, out var hero))
                {
                    pendingExecutions.Remove(heroId);
                    continue;
                }
                if (!HeroExecutionRules.IsExecutable(hero, out _)) continue;

                PartyBase captorParty = hero.PartyBelongedToAsPrisoner;
                if (hero.IsPrisoner && captorParty?.LeaderHero != null && captorParty.MapEvent == null &&
                    captorParty.LeaderHero.Clan == player.Clan && MBRandom.RandomFloat <= 0.125f)
                {
                    KillCharacterAction.ApplyByExecution(hero, captorParty.LeaderHero);
                    return;
                }
            }
        }
    }

    public void CanHeroBeReleased(Hero hero, ref bool result)
    {
        // Holding a captive the host's config never lets anyone execute would keep them captive for good
        if (!HeroExecutionRules.IsExecutable(hero, out _)) return;
        if (!objectManager.TryGetId(hero, out var heroId)) return;

        if (Data.PlayerPendingSettlementExecutions.Values.Any(pendingExecutions => pendingExecutions.ContainsKey(heroId)) ||
            Data.PlayerHeroesPendingMapEventEndExecution.Values.Any(pendingExecutions => pendingExecutions.Contains(heroId)))
        {
            result = false;
            return;
        }

        PartyBase captorParty = hero.PartyBelongedToAsPrisoner;
        if (!hero.IsPrisoner || captorParty?.IsMobile != true || captorParty.LeaderHero == null) return;

        Clan captorClan = captorParty.MobileParty.ActualClan;
        if (GetPlayers().Any(player => hero.Clan == player.Clan && HasBloodFeud(player.HeroId, captorClan)))
        {
            result = false;
        }
    }

    public void SettleBloodFeudByRansomPayment(Hero playerHero, Clan clan)
    {
        var player = GetPlayers().FirstOrDefault(candidate => candidate.Hero == playerHero);
        if (player.Hero == null) return;
        if (!TryGetBloodFeud(player.HeroId, clan, out var feud) || !feud.HasBloodFeud) return;

        // The client priced it from its last counts, which can be behind the server's
        int bloodMoney = GetBloodMoneyForPlayerToPayAgainstClan(feud);
        if (playerHero.Gold < bloodMoney) return;

        GiveGoldAction.ApplyBetweenCharacters(playerHero, null, bloodMoney);
        ChangeBloodFeudState(player, clan, null, ChangeBloodFeudActionDetail.SettledByRansomPayment);
        ChangeRelationAction.SetRelationBetweenHeroes(player.Clan.Leader, clan.Leader, -49);
    }

    public void RequestSettleBloodFeudByRansomPayment(Clan clan)
    {
        if (!objectManager.TryGetIdWithLogging(clan, out var clanId)) return;

        network.SendAll(new NetworkSettleBloodFeudByRansomPayment(clanId));
    }

    public bool HasBloodFeud(Hero playerHero, Clan clan)
    {
        return objectManager.TryGetId(playerHero, out var playerHeroId) && HasBloodFeud(playerHeroId, clan);
    }

    public bool HasBloodFeudWithAnyPlayer(Clan clan)
    {
        return GetPlayers().Any(player => HasBloodFeud(player.HeroId, clan));
    }

    public void SetPendingPlayerExecution(Hero playerHero, Hero executor)
    {
        if (!objectManager.TryGetIdWithLogging(playerHero, out var playerHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(executor, out var executorId)) return;

        pendingPlayerExecutions[playerHeroId] = executorId;
    }

    public void ExecutePendingPlayers()
    {
        if (pendingPlayerExecutions.Count == 0) return;

        foreach (var pendingExecution in pendingPlayerExecutions.ToArray())
        {
            pendingPlayerExecutions.Remove(pendingExecution.Key);

            if (!objectManager.TryGetObjectWithLogging<Hero>(pendingExecution.Key, out var playerHero)) continue;
            if (!objectManager.TryGetObjectWithLogging<Hero>(pendingExecution.Value, out var executor)) continue;

            if (playerHero.IsPrisoner)
                KillCharacterAction.ApplyByExecution(playerHero, executor);
        }
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

    private void OnPlayerExecutedHero(BloodFeudPlayer player, Hero victim)
    {
        // KillCharacterActionPatches charges the trait penalty, since it runs before this event
        if (!HasBloodFeud(player.HeroId, victim.Clan))
        {
            ChangeBloodFeudState(player, victim.Clan, victim, ChangeBloodFeudActionDetail.StartedByPlayerExecuteAHero);
        }
    }

    private void OnPlayerClanMemberExecuted(BloodFeudPlayer player, Hero executedHero, Hero executor)
    {
        if (!HasBloodFeud(player.HeroId, executor.Clan))
        {
            ChangeBloodFeudState(player, executor.Clan, executedHero, ChangeBloodFeudActionDetail.StartedByAIExecutePlayerRelative);
        }

        if (executedHero.IsPlayerHero()) return;
        if (!objectManager.TryGetIdWithLogging(executedHero, out var executedHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(executor, out var executorId)) return;

        SendToPlayer(player.Player, new NetworkNotifyBloodFeudClanMemberExecuted(executedHeroId, executorId));
    }

    private void OnPlayerMemberExecutedAHero(BloodFeudPlayer player, Hero heroToExecute, Hero clanMember)
    {
        if (!objectManager.TryGetIdWithLogging(heroToExecute, out var executedHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(clanMember, out var clanMemberId)) return;

        SendToPlayer(player.Player, new NetworkNotifyBloodFeudLordExecuted(executedHeroId, clanMemberId));
    }

    // ChangeBloodFeudStateAction.ApplyInternal and OnBloodFeudStateChanged for one player
    private void ChangeBloodFeudState(BloodFeudPlayer player, Clan clanWithFeud, Hero executedHero, ChangeBloodFeudActionDetail detail)
    {
        if (!objectManager.TryGetIdWithLogging(clanWithFeud, out var clanId)) return;

        BloodFeudData feud = GetOrAddBloodFeud(player.HeroId, clanId);
        feud.HasBloodFeud = detail == ChangeBloodFeudActionDetail.StartedByPlayerExecuteAHero || detail == ChangeBloodFeudActionDetail.StartedByAIExecutePlayerRelative;
        if (detail == ChangeBloodFeudActionDetail.SettledByRansomPayment)
        {
            feud.ExecutionsDoneCount = 0;
            feud.ExecutionsReceivedCount = 0;
        }
        SendBloodFeuds(player.Player);

        int penalizedClanCount = 0;
        if (feud.HasBloodFeud)
        {
            Hero playerClanLeader = player.Clan.Leader;
            ChangeRelationAction.SetRelationBetweenHeroes(playerClanLeader, clanWithFeud.Leader, Campaign.Current.Models.DiplomacyModel.MinRelationLimit);
            foreach (Clan clan in Clan.All)
            {
                if (clan == player.Clan) continue;

                int relationPenalty = ExecutionCampaignBehavior.GetBloodFeudStartRelationPenaltyToOtherClan(executedHero, clan);
                if (relationPenalty == 0) continue;

                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(playerClanLeader, clan.Leader, relationPenalty, showQuickNotification: false);
                penalizedClanCount++;
            }
        }

        objectManager.TryGetId(executedHero, out var executedHeroId);
        SendToPlayer(player.Player, new NetworkNotifyBloodFeudStateChanged(clanId, executedHeroId, detail, penalizedClanCount));

        UpdateAllPlayerClanMemberExecutionStates(player);
    }

    private void UpdateAllPlayerClanMemberExecutionStates(BloodFeudPlayer player)
    {
        // Nothing can start or cancel without a feud or a pending execution
        bool hasBloodFeud = Data.PlayerBloodFeuds.TryGetValue(player.HeroId, out var feuds) && feuds.Values.Any(feud => feud.HasBloodFeud);
        bool hasPendingExecution = Data.PlayerPendingSettlementExecutions.TryGetValue(player.HeroId, out var pendingExecutions) && pendingExecutions.Count > 0;
        if (!hasBloodFeud && !hasPendingExecution) return;

        foreach (Hero hero in player.Clan.Heroes.ToList())
        {
            if (!hero.IsPlayerHero())
            {
                UpdateClanMemberExecutionState(player, hero);
            }
        }
    }

    private void UpdateClanMemberExecutionState(BloodFeudPlayer player, Hero clanMember)
    {
        Clan pendingClan = null;
        if (TryGetPendingSettlementExecution(player.HeroId, clanMember, out var pendingExecution))
        {
            objectManager.TryGetObjectWithLogging(pendingExecution.CapturerClanId, out pendingClan);
        }

        if (HeroExecutionRules.IsExecutable(clanMember, out _) &&
            GetClanMemberCurrentExecutionData(player, clanMember, pendingClan, out var currentExecutorClan))
        {
            if (pendingClan == null)
            {
                StartClanMemberExecutionAtSettlement(player, clanMember, currentExecutorClan);
            }
            else if (pendingClan != currentExecutorClan)
            {
                CancelClanMemberExecutionAtSettlement(player, clanMember, pendingClan);
                StartClanMemberExecutionAtSettlement(player, clanMember, currentExecutorClan);
            }
        }
        else if (pendingClan != null)
        {
            CancelClanMemberExecutionAtSettlement(player, clanMember, pendingClan);
        }
    }

    private bool GetClanMemberCurrentExecutionData(BloodFeudPlayer player, Hero hero, Clan pendingClan, out Clan currentExecutorClan)
    {
        Settlement prison = hero.PartyBelongedToAsPrisoner?.Settlement;
        if (pendingClan != null && HasBloodFeud(player.HeroId, pendingClan) && prison?.OwnerClan.MapFaction == pendingClan.MapFaction && !pendingClan.IsEliminated)
        {
            currentExecutorClan = pendingClan;
            return true;
        }

        if (prison != null)
        {
            currentExecutorClan = prison.OwnerClan;
            if (HasBloodFeud(player.HeroId, currentExecutorClan))
            {
                return true;
            }
        }

        currentExecutorClan = null;
        return false;
    }

    private void StartClanMemberExecutionAtSettlement(BloodFeudPlayer player, Hero clanMember, Clan clanWithFeud)
    {
        Settlement settlement = clanMember.PartyBelongedToAsPrisoner.Settlement;
        if (!objectManager.TryGetIdWithLogging(clanMember, out var clanMemberId)) return;
        if (!objectManager.TryGetIdWithLogging(clanWithFeud, out var clanId)) return;
        if (!objectManager.TryGetIdWithLogging(settlement, out var settlementId)) return;

        CampaignTime executionDate = CampaignTime.DaysFromNow(ExecutionCampaignBehavior.DaysTillBloodFeudExecutionInSettlement);
        if (!Data.PlayerPendingSettlementExecutions.TryGetValue(player.HeroId, out var pendingExecutions))
        {
            pendingExecutions = new Dictionary<string, PendingSettlementExecution>();
            Data.PlayerPendingSettlementExecutions[player.HeroId] = pendingExecutions;
        }
        pendingExecutions[clanMemberId] = new PendingSettlementExecution(clanId, executionDate.NumTicks);

        SendToPlayer(player.Player, new NetworkNotifyBloodFeudSettlementExecutionStarted(clanMemberId, clanId, settlementId, executionDate.NumTicks));
    }

    private void CancelClanMemberExecutionAtSettlement(BloodFeudPlayer player, Hero clanMember, Clan capturerClan)
    {
        if (!objectManager.TryGetIdWithLogging(clanMember, out var clanMemberId)) return;
        if (!objectManager.TryGetIdWithLogging(capturerClan, out var clanId)) return;

        Data.PlayerPendingSettlementExecutions[player.HeroId].Remove(clanMemberId);

        SendToPlayer(player.Player, new NetworkNotifyBloodFeudSettlementExecutionCancelled(clanMemberId, clanId));
    }

    private float CalculatePlayerClanMemberExecutionProbability(List<BloodFeudPlayer> clanPlayers, Hero clanMember, Hero executor)
    {
        if (clanPlayers.Any(player => HasBloodFeud(player.HeroId, executor.Clan)))
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

    private void SendBloodFeuds(Player player)
    {
        Data.PlayerBloodFeuds.TryGetValue(player.HeroId, out var feuds);
        SendToPlayer(player, new NetworkUpdateBloodFeuds(feuds));
    }

    private void SendToPlayer(Player player, IMessage message)
    {
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;

        network.Send(peer, message);
    }

    private void AddPendingMapEventEndExecution(string playerHeroId, Hero hero)
    {
        if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) return;

        if (!Data.PlayerHeroesPendingMapEventEndExecution.TryGetValue(playerHeroId, out var pendingExecutions))
        {
            pendingExecutions = new List<string>();
            Data.PlayerHeroesPendingMapEventEndExecution[playerHeroId] = pendingExecutions;
        }
        pendingExecutions.Add(heroId);
    }

    private bool TryGetPendingSettlementExecution(string playerHeroId, Hero hero, out PendingSettlementExecution pendingExecution)
    {
        pendingExecution = null;
        return objectManager.TryGetId(hero, out var heroId) &&
            Data.PlayerPendingSettlementExecutions.TryGetValue(playerHeroId, out var pendingExecutions) &&
            pendingExecutions.TryGetValue(heroId, out pendingExecution);
    }

    private bool HasBloodFeud(string playerHeroId, Clan clan)
    {
        return TryGetBloodFeud(playerHeroId, clan, out var feud) && feud.HasBloodFeud;
    }

    private bool TryGetBloodFeud(string playerHeroId, Clan clan, out BloodFeudData feud)
    {
        feud = null;
        return clan != null &&
            objectManager.TryGetId(clan, out var clanId) &&
            Data.PlayerBloodFeuds.TryGetValue(playerHeroId, out var feuds) &&
            feuds.TryGetValue(clanId, out feud);
    }

    private BloodFeudData GetOrAddBloodFeud(string playerHeroId, string clanId)
    {
        if (!Data.PlayerBloodFeuds.TryGetValue(playerHeroId, out var feuds))
        {
            feuds = new Dictionary<string, BloodFeudData>();
            Data.PlayerBloodFeuds[playerHeroId] = feuds;
        }

        if (!feuds.TryGetValue(clanId, out var feud))
        {
            feud = new BloodFeudData();
            feuds[clanId] = feud;
        }
        return feud;
    }

    private List<BloodFeudPlayer> GetPlayers()
    {
        var players = new List<BloodFeudPlayer>();
        foreach (var player in playerManager.Players)
        {
            if (!objectManager.TryGetObject<Hero>(player.HeroId, out var hero) || hero.Clan == null) continue;

            players.Add(new BloodFeudPlayer(player, hero));
        }
        return players;
    }

    private readonly struct BloodFeudPlayer
    {
        public readonly Player Player;
        public readonly Hero Hero;

        public string HeroId => Player.HeroId;
        public Clan Clan => Hero.Clan;

        public BloodFeudPlayer(Player player, Hero hero)
        {
            Player = player;
            Hero = hero;
        }
    }
}
