using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Alleys.Handlers;

/// <summary>
/// Server-authoritative alley replication. The owner-change path turns the local
/// <see cref="AlleyOwnerChanged"/> into a networked <see cref="ChangeAlleyOwner"/> the clients replay
/// (keeping owner, derived <c>State</c> and <c>OwnedAlleys</c> consistent). The rest runs the vanilla
/// daily alley simulation and AI attack/defense: the gated vanilla handlers publish local triggers (see
/// AlleyCampaignBehaviorPatches), and the server applies them over the CoopSession-tracked player alleys
/// (the host's own <c>_playerOwnedCommonAreaData</c> is empty) with patches live, so each side effect -
/// owner change, garrison edit, overseer XP, destroy, attack spawn - replicates through the existing sync
/// and RNG is rolled once here, never per client. An attack is sent to the owning client, which fights it
/// locally (like the take-over) and reports the result back for the server to adjudicate; an unanswered
/// attack times out in the daily tick.
/// </summary>
internal class AlleyHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AlleyHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionAlleyPlayerDataInterface sessionInterface;
    private readonly IAlleyCampaignBehaviorInterface behaviorInterface;

    public AlleyHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionAlleyPlayerDataInterface sessionInterface,
        IAlleyCampaignBehaviorInterface behaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInterface = sessionInterface;
        this.behaviorInterface = behaviorInterface;

        messageBroker.Subscribe<AlleyOwnerChanged>(Handle_AlleyOwnerChanged);
        messageBroker.Subscribe<ChangeAlleyOwner>(Handle_ChangeAlleyOwner);

        messageBroker.Subscribe<AlleyDailyTickTriggered>(Handle_DailyTick);
        messageBroker.Subscribe<AlleyDailyTickSettlementTriggered>(Handle_DailyTickSettlement);
        messageBroker.Subscribe<AlleyHeroKilledTriggered>(Handle_HeroKilled);

        messageBroker.Subscribe<AlleyDefenseResolvedRequested>(Handle_AlleyDefenseResolvedRequested);
        messageBroker.Subscribe<NetworkAlleyUnderAttack>(Handle_NetworkAlleyUnderAttack);
        messageBroker.Subscribe<RequestAlleyDefenseResolved>(Handle_RequestAlleyDefenseResolved);
        messageBroker.Subscribe<ForceAlleyAttackRequested>(Handle_ForceAlleyAttack);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AlleyOwnerChanged>(Handle_AlleyOwnerChanged);
        messageBroker.Unsubscribe<ChangeAlleyOwner>(Handle_ChangeAlleyOwner);

        messageBroker.Unsubscribe<AlleyDailyTickTriggered>(Handle_DailyTick);
        messageBroker.Unsubscribe<AlleyDailyTickSettlementTriggered>(Handle_DailyTickSettlement);
        messageBroker.Unsubscribe<AlleyHeroKilledTriggered>(Handle_HeroKilled);

        messageBroker.Unsubscribe<AlleyDefenseResolvedRequested>(Handle_AlleyDefenseResolvedRequested);
        messageBroker.Unsubscribe<NetworkAlleyUnderAttack>(Handle_NetworkAlleyUnderAttack);
        messageBroker.Unsubscribe<RequestAlleyDefenseResolved>(Handle_RequestAlleyDefenseResolved);
        messageBroker.Unsubscribe<ForceAlleyAttackRequested>(Handle_ForceAlleyAttack);
    }

    private static AlleyModel Model => Campaign.Current?.Models?.AlleyModel;

    // --- Owner replication ---

    private void Handle_AlleyOwnerChanged(MessagePayload<AlleyOwnerChanged> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;

        if (!objectManager.TryGetIdWithLogging(data.Alley, out var alleyId)) return;

        // A null new owner (alley vacated) is valid and serializes as a null id.
        string newOwnerId = null;
        if (data.NewOwner != null && !objectManager.TryGetIdWithLogging(data.NewOwner, out newOwnerId)) return;

        network.SendAll(new ChangeAlleyOwner(alleyId, newOwnerId));
    }

    private void Handle_ChangeAlleyOwner(MessagePayload<ChangeAlleyOwner> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;

        // Resolve ids inside the game-thread closure: the alley/hero may be registered by an earlier
        // handler that also defers to the game thread, so a poll-thread lookup could miss it.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Alley>(data.AlleyId, out var alley)) return;

            Hero newOwner = null;
            if (data.NewOwnerId != null && !objectManager.TryGetObjectWithLogging(data.NewOwnerId, out newOwner)) return;

            // Receive/apply path: replay SetOwner with patches stood down so it doesn't re-announce.
            using (new AllowedThread())
            {
                alley.SetOwner(newOwner);
            }
        });
    }

    // --- Daily simulation ---

    // Caller is game thread
    private void Handle_DailyTick(MessagePayload<AlleyDailyTickTriggered> payload)
    {
        if (ModInformation.IsClient) return;

        foreach (var alley in behaviorInterface.GetPlayerOwnedAlleys())
        {
            if (!objectManager.TryGetId(alley, out var alleyId)) continue;
            if (!sessionInterface.TryGetManagementData(alleyId, out var data)) continue;

            // Isolate per-alley failures so one bad alley doesn't skip the rest of the day's tick.
            try
            {
                RunDailyAlleyTick(alley, alleyId, data);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Alley daily tick failed for {alley}", alleyId);
            }
        }
    }

    private void RunDailyAlleyTick(Alley alley, string alleyId, AlleyManagementData data)
    {
        ConvertTroopsToBandits(alleyId, data);

        // Conversion may have replaced the stored entry; re-read so the XP and attack-deadline steps see
        // the current garrison rather than the pre-conversion one.
        if (!sessionInterface.TryGetManagementData(alleyId, out data)) return;

        GrantDailyXp(alley, data);

        var overseer = ResolveOverseer(data);
        bool overseerDead = overseer != null && overseer.IsDead;
        bool underAttack = data.UnderAttackByAlleyId != null;

        if (overseerDead && overseer.DeathDay + Model.DestroyAlleyAfterDaysWhenLeaderIsDeath < CampaignTime.Now)
        {
            DestroyPlayerAlley(alley, alleyId, overseer, attacker: null);
        }
        else if (!underAttack && overseer != null && !overseerDead)
        {
            CheckSpawnAttack(alley, alleyId, data);
        }
        else if (underAttack && data.AttackResponseDueDate.IsPast)
        {
            // The owner never answered by the deadline (they may be offline): the attacking gang takes it.
            objectManager.TryGetObjectWithLogging<Alley>(data.UnderAttackByAlleyId, out var attacker);
            DestroyPlayerAlley(alley, alleyId, overseer, attacker);
        }
    }

    /// <summary>
    /// Vanilla CheckConvertTroopsToBandits, run on the stored garrison snapshot (there is no live alley
    /// roster on the host): each non-hero, non-gangster troop has a 1% daily chance to turn into a thug of
    /// at least its own tier. Only broadcasts when something actually changed.
    /// </summary>
    private void ConvertTroopsToBandits(string alleyId, AlleyManagementData data)
    {
        var garrison = data.Garrison;
        if (garrison == null || garrison.Length == 0) return;

        if (!TryGetThug("gangster_1", out var thug, out var thugId) ||
            !TryGetThug("gangster_2", out var expertThug, out var expertId) ||
            !TryGetThug("gangster_3", out var masterThug, out var masterId)) return;

        var result = new List<TroopRosterElementData>();
        int addThug = 0, addExpert = 0, addMaster = 0;
        bool changed = false;

        foreach (var element in garrison)
        {
            if (!objectManager.TryGetObject<CharacterObject>(element.CharacterId, out var character) ||
                character.IsHero || character.Occupation == Occupation.Gangster)
            {
                result.Add(element);
                continue;
            }

            int converted = 0;
            for (int i = 0; i < element.Number; i++)
            {
                if (MBRandom.RandomFloat < 0.01f) converted++;
            }
            if (converted == 0)
            {
                result.Add(element);
                continue;
            }

            changed = true;
            int remaining = element.Number - converted;
            if (remaining > 0)
            {
                int wounded = element.WoundedNumber < remaining ? element.WoundedNumber : remaining;
                result.Add(new TroopRosterElementData(element.CharacterId, remaining, wounded, element.Xp));
            }

            // Same tier ladder as vanilla: the replacement thug is at least the converted troop's tier.
            if (thug.Tier >= character.Tier) addThug += converted;
            else if (expertThug.Tier >= character.Tier) addExpert += converted;
            else addMaster += converted;
        }

        if (!changed) return;

        AddTroopCount(result, thugId, addThug);
        AddTroopCount(result, expertId, addExpert);
        AddTroopCount(result, masterId, addMaster);

        var newGarrison = result.ToArray();
        sessionInterface.SetManagementData(alleyId, data.OverseerId, newGarrison);
        network.SendAll(new NetworkAlleyManagementUpdated(alleyId, data.OverseerId, newGarrison));
    }

    /// <summary>
    /// Vanilla SkillLevelingManager.OnDailyAlleyTick, minus the Hero.MainHero deref that NREs on the host:
    /// the owner and the overseer both gain daily Roguery XP, captured and replicated by the XP sync.
    /// </summary>
    private void GrantDailyXp(Alley alley, AlleyManagementData data)
    {
        var model = Model;
        if (model == null) return;

        if (alley.Owner != null && !alley.Owner.IsGangLeader)
        {
            alley.Owner.AddSkillXp(DefaultSkills.Roguery, model.GetDailyXpGainForMainHero());
        }

        var overseer = ResolveOverseer(data);
        if (overseer != null && !overseer.IsDead)
        {
            overseer.AddSkillXp(DefaultSkills.Roguery, model.GetDailyXpGainForAssignedClanMember(overseer));
        }
    }

    private void CheckSpawnAttack(Alley alley, string alleyId, AlleyManagementData data)
    {
        if (MBRandom.RandomFloat >= 0.015f) return;
        StartAttack(alley, alleyId, data);
    }

    /// <summary>
    /// Vanilla StartNewAlleyAttack, keyed off the alley owner rather than Hero.MainHero: pick a rival
    /// gang-occupied alley in the same settlement (RNG rolled once here), set the response deadline, and
    /// tell the owning client so its confront-alley menu/conversation/fight light up.
    /// </summary>
    private void StartAttack(Alley alley, string alleyId, AlleyManagementData data)
    {
        var settlement = alley.Settlement;
        if (settlement?.Alleys == null) return;

        // Gang-occupied rivals only. On the host, State can't distinguish them - a player alley's owner
        // isn't the host's (null) main hero, so it also reads OccupiedByGangLeader - so key off the owner
        // being a gang leader, and never attack from the alley itself.
        var rivals = settlement.Alleys.Where(a => a != alley && a.Owner != null && a.Owner.IsGangLeader).ToList();
        if (rivals.Count == 0) return;

        var attacker = rivals[MBRandom.RandomInt(0, rivals.Count)];
        if (attacker == null || !objectManager.TryGetId(attacker, out var attackerId)) return;

        var responseRoster = AlleyGarrisonData.FromData(data.Garrison, objectManager);
        var dueDate = CampaignTime.DaysFromNow(Model.GetAlleyAttackResponseTimeInDays(responseRoster));

        sessionInterface.SetUnderAttackByAi(alleyId, attackerId, dueDate);

        // Same relation hit vanilla applies to the attacking gang leader, keyed off the alley owner.
        if (alley.Owner != null && attacker.Owner != null)
        {
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(alley.Owner, attacker.Owner, -5, showQuickNotification: false);
        }

        network.SendAll(new NetworkAlleyUnderAttack(alleyId, attackerId, dueDate));
    }

    /// <summary>
    /// Vanilla PlayerAlleyData.DestroyAlley: make a living overseer a fugitive, hand the alley to the
    /// attacking gang leader (or clear it if none), and drop the garrison. Owner change and fugitive state
    /// replicate on their own; the garrison drop is the management-data removal.
    /// </summary>
    private void DestroyPlayerAlley(Alley alley, string alleyId, Hero overseer, Alley attacker)
    {
        if (overseer != null && overseer.IsAlive && overseer.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
        {
            MakeHeroFugitiveAction.Apply(overseer, false);
        }

        alley.SetOwner(attacker?.Owner);

        sessionInterface.RemoveManagementData(alleyId);
        network.SendAll(new NetworkAlleyManagementRemoved(alleyId));
    }

    // --- Gang-vs-gang ownership ---

    // Caller is game thread
    private void Handle_DailyTickSettlement(MessagePayload<AlleyDailyTickSettlementTriggered> payload)
    {
        if (ModInformation.IsClient) return;

        var settlement = payload.What.Settlement;
        if (settlement?.Notables == null || settlement.Alleys == null) return;

        foreach (var notable in settlement.Notables)
        {
            if (notable == null || !notable.IsGangLeader) continue;

            // Isolate per-notable failures so one bad gang leader doesn't skip the rest of the settlement.
            try
            {
                TickGangLeader(settlement, notable);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Alley gang tick failed for {notable}", notable.StringId);
            }
        }
    }

    /// <summary>
    /// Vanilla TickAlleyOwnerships for one gang-leader notable: randomly claim an empty alley or drop one
    /// it owns, and heal. The "don't drop an alley it's using to attack a player" guard reads the CoopSession
    /// (the host's <c>_playerOwnedCommonAreaData</c> is empty). Each SetOwner replicates via the owner path.
    /// </summary>
    private void TickGangLeader(Settlement settlement, Hero notable)
    {
        int count = notable.OwnedAlleys.Count;
        float gainChance = 0.02f - (count * 0.005f);
        float loseChance = count * 0.005f;

        if (MBRandom.RandomFloat < gainChance)
        {
            var empty = settlement.Alleys.FirstOrDefault(a => a.State == Alley.AreaState.Empty);
            empty?.SetOwner(notable);
        }

        if (MBRandom.RandomFloat < loseChance && !IsNotableAttackingPlayerAlley(notable, settlement))
        {
            var owned = notable.OwnedAlleys.Count > 0 ? notable.OwnedAlleys[MBRandom.RandomInt(0, notable.OwnedAlleys.Count)] : null;
            owned?.SetOwner(null);
        }

        if (!notable.IsHealthFull()) notable.Heal(10, false);
    }

    private bool IsNotableAttackingPlayerAlley(Hero notable, Settlement settlement)
    {
        foreach (var alley in settlement.Alleys)
        {
            // Only player-owned alleys carry an under-attack entry; skip empty and gang-occupied ones.
            if (alley?.Owner == null || alley.Owner.IsGangLeader) continue;
            if (!objectManager.TryGetId(alley, out var alleyId)) continue;
            if (!sessionInterface.TryGetManagementData(alleyId, out var data) || data.UnderAttackByAlleyId == null) continue;
            if (objectManager.TryGetObject<Alley>(data.UnderAttackByAlleyId, out var attacker) && attacker.Owner == notable) return true;
        }
        return false;
    }

    // --- Hero killed ---

    // Caller is game thread
    private void Handle_HeroKilled(MessagePayload<AlleyHeroKilledTriggered> payload)
    {
        if (ModInformation.IsClient) return;

        var victim = payload.What.Victim;
        if (victim == null) return;

        try
        {
            ApplyHeroKilledToAlleys(victim);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Alley hero-killed handling failed for {victim}", victim.StringId);
        }
    }

    /// <summary>
    /// Vanilla OnHeroKilled: an attack by a now-dead gang leader is called off, and (for a gang-leader
    /// victim) every gang alley it owned is freed. A dead overseer isn't handled here - the daily tick
    /// already destroys a dead-leader alley after the grace period, keyed off the stored overseer id.
    /// </summary>
    private void ApplyHeroKilledToAlleys(Hero victim)
    {
        foreach (var alley in behaviorInterface.GetPlayerOwnedAlleys())
        {
            // Isolate per-alley failures so one bad alley doesn't skip the rest.
            try
            {
                ApplyHeroKilledToAlley(alley, victim);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Alley hero-killed handling failed for an alley (victim {victim})", victim.StringId);
            }
        }

        // Only a gang leader's own alleys are freed on death. A player hero (or any non-gang-leader) keeps
        // them; Clan.PlayerClan can't identify a client's clan on the host, so key off the gang-leader role.
        if (!victim.IsGangLeader) return;

        foreach (var alley in victim.OwnedAlleys.ToList())
        {
            alley.SetOwner(null);
        }
    }

    private void ApplyHeroKilledToAlley(Alley alley, Hero victim)
    {
        if (!objectManager.TryGetId(alley, out var alleyId)) return;
        if (!sessionInterface.TryGetManagementData(alleyId, out var data)) return;

        // A now-dead gang leader can no longer press its attack on this alley.
        if (data.UnderAttackByAlleyId != null &&
            objectManager.TryGetObject<Alley>(data.UnderAttackByAlleyId, out var attacker) && attacker.Owner == victim)
        {
            sessionInterface.ClearUnderAttackByAi(alleyId);
            network.SendAll(new NetworkAlleyUnderAttack(alleyId, null, default));
        }
    }

    // --- AI attack/defense flow ---

    private void Handle_NetworkAlleyUnderAttack(MessagePayload<NetworkAlleyUnderAttack> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Alley>(data.AlleyId, out var alley)) return;

            // Only the owning client tracks this alley's under-attack state; its confront menu keys off it.
            if (alley.Owner != Hero.MainHero) return;

            if (data.AttackerAlleyId == null)
            {
                behaviorInterface.ClearPlayerAlleyUnderAttackByAi(alley);
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<Alley>(data.AttackerAlleyId, out var attacker)) return;
            behaviorInterface.SetPlayerAlleyUnderAttackByAi(alley, attacker, data.DueDate, showNotification: true);
        });
    }

    // Caller is game thread
    private void Handle_AlleyDefenseResolvedRequested(MessagePayload<AlleyDefenseResolvedRequested> payload)
    {
        if (ModInformation.IsServer) return;

        var alley = payload.What.Alley;
        var won = payload.What.Won;
        if (alley == null || !objectManager.TryGetIdWithLogging(alley, out var alleyId)) return;

        // The fight is over: clear the local under-attack state and switch to the vanilla result menu the
        // patched AlleyFightWon/Lost would have (their body is skipped so the outcome comes from the server).
        behaviorInterface.ClearPlayerAlleyUnderAttackByAi(alley);
        GameMenu.SwitchToMenu(won ? "alley_fight_won" : "alley_fight_lost");

        // On a win, forward the post-fight garrison so the server records the defenders lost in the fight.
        var garrison = payload.What.Garrison != null
            ? AlleyGarrisonData.ToData(payload.What.Garrison, objectManager)
            : Array.Empty<TroopRosterElementData>();
        network.SendAll(new RequestAlleyDefenseResolved(alleyId, won, garrison));
    }

    private void Handle_RequestAlleyDefenseResolved(MessagePayload<RequestAlleyDefenseResolved> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Alley>(data.AlleyId, out var alley)) return;
            if (!sessionInterface.TryGetManagementData(data.AlleyId, out var mgmt)) return;

            // Ignore a stale/duplicate resolve: the attack may already have timed out and destroyed the alley.
            if (mgmt.UnderAttackByAlleyId == null) return;

            objectManager.TryGetObject<Alley>(mgmt.UnderAttackByAlleyId, out var attacker);

            if (data.Won) ResolveDefenseWon(alley, data.AlleyId, attacker, mgmt.OverseerId, data.Garrison);
            else DestroyPlayerAlley(alley, data.AlleyId, ResolveOverseer(mgmt), attacker);
        });
    }

    /// <summary>
    /// Vanilla AlleyFightWon, keyed off the alley owner: the attacking gang leader loses 20% power and its
    /// alley, the attack clears, and the owner gains the defense-win Roguery XP (replicated by the XP sync).
    /// </summary>
    private void ResolveDefenseWon(Alley alley, string alleyId, Alley attacker, string overseerId, TroopRosterElementData[] garrison)
    {
        if (attacker != null)
        {
            var attackerOwner = attacker.Owner;
            attackerOwner?.AddPower(-(attackerOwner.Power * 0.2f));
            attacker.SetOwner(null);
        }

        sessionInterface.ClearUnderAttackByAi(alleyId);

        // Record the post-fight garrison so the server's roster matches the client and a later management
        // update or rejoin doesn't restore the defenders that died in the fight.
        if (garrison != null)
        {
            sessionInterface.SetManagementData(alleyId, overseerId, garrison);
            network.SendAll(new NetworkAlleyManagementUpdated(alleyId, overseerId, garrison));
        }

        if (alley.Owner != null)
        {
            alley.Owner.AddSkillXp(DefaultSkills.Roguery, Model.GetXpGainAfterSuccessfulAlleyDefenseForMainHero());
        }
    }

    // Debug cheat entry: force an attack on a specific player alley now, bypassing the daily 1.5% roll.
    // Caller is game thread
    private void Handle_ForceAlleyAttack(MessagePayload<ForceAlleyAttackRequested> payload)
    {
        if (ModInformation.IsClient) return;

        var alley = payload.What.Alley;
        if (alley == null || !objectManager.TryGetId(alley, out var alleyId)) return;
        if (!sessionInterface.TryGetManagementData(alleyId, out var data)) return;

        try
        {
            StartAttack(alley, alleyId, data);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Forced alley attack failed");
        }
    }

    private Hero ResolveOverseer(AlleyManagementData data)
    {
        if (data?.OverseerId == null) return null;
        objectManager.TryGetObject<Hero>(data.OverseerId, out var overseer);
        return overseer;
    }

    private bool TryGetThug(string stringId, out CharacterObject character, out string id)
    {
        id = null;
        character = MBObjectManager.Instance?.GetObject<CharacterObject>(stringId);
        if (character == null) return false;
        return objectManager.TryGetId(character, out id);
    }

    private static void AddTroopCount(List<TroopRosterElementData> roster, string characterId, int count)
    {
        if (count <= 0 || characterId == null) return;

        for (int i = 0; i < roster.Count; i++)
        {
            if (roster[i].CharacterId == characterId)
            {
                roster[i] = new TroopRosterElementData(characterId, roster[i].Number + count, roster[i].WoundedNumber, roster[i].Xp);
                return;
            }
        }
        roster.Add(new TroopRosterElementData(characterId, count, 0, 0));
    }
}
